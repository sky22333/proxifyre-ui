using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace proxifyre_ui
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly string ProgramPath = AppDomain.CurrentDomain.BaseDirectory + Constants.ProgramName;
        private readonly string ConfigPath = AppDomain.CurrentDomain.BaseDirectory + "app-config.json";
        private const int MaxLogLines = 500;
        private readonly ConcurrentQueue<string> pendingLogLines = new ConcurrentQueue<string>();
        private readonly DispatcherTimer logFlushTimer;
        private readonly DataReceivedEventHandler processOutputHandler;
        private readonly DataReceivedEventHandler processErrorHandler;
        private readonly EventHandler processExitedHandler;

        [ObservableProperty]
        private ObservableCollection<ProxyViewModel> proxies = new ObservableCollection<ProxyViewModel>();

        [ObservableProperty]
        private ProxyViewModel selectedProxy;

        [ObservableProperty]
        private ObservableCollection<string> logLines = new ObservableCollection<string>();

        [ObservableProperty]
        private bool isRunning;

        private Process proxifyreProcess;
        
        // 核心子进程生命周期管理器 (防御孤儿进程)
        private readonly ChildProcessTracker processTracker = new ChildProcessTracker();

        [ObservableProperty]
        private string manualAppInput = string.Empty;

        public MainViewModel()
        {
            processOutputHandler = OnProcessOutputDataReceived;
            processErrorHandler = OnProcessErrorDataReceived;
            processExitedHandler = OnProcessExited;
            logFlushTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            logFlushTimer.Tick += FlushPendingLogs;
            logFlushTimer.Start();
            LoadConfig();
            CheckEnvironmentAsync();
        }

        private async void CheckEnvironmentAsync()
        {
            // Delay to ensure the Main Window is fully rendered and the Visual Tree is ready
            await Task.Delay(1500); 

            var missingDeps = EnvironmentDetector.CheckMissingDependencies();
            if (missingDeps.Count > 0 && Application.Current.MainWindow is MainWindow mainWindow)
            {
                var dialogPresenter = mainWindow.FindName("RootContentDialogPresenter") as System.Windows.Controls.ContentPresenter;
                if (dialogPresenter == null) return;

                var dialog = new DependencyDownloadDialog(dialogPresenter);
                var vm = new DependencyDownloadViewModel(missingDeps, this);
                dialog.DataContext = vm;
                Action closeDialog = null;
                closeDialog = () => dialog.Hide();
                vm.OnCloseRequested += closeDialog;

                try
                {
                    await dialog.ShowAsync();
                }
                catch (Exception ex)
                {
                    // Handle case where dialog is already open or presenter is invalid
                    Debug.WriteLine($"Failed to show dialog: {ex.Message}");
                }
                finally
                {
                    vm.OnCloseRequested -= closeDialog;
                    vm.Stop();
                    dialog.DataContext = null;
                }
            }
        }

        private void LoadConfig()
        {
            if (File.Exists(ConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigPath);
                    var config = JsonConvert.DeserializeObject<Configuration>(json);
                    if (config != null)
                    {
                        if (config.Proxies != null)
                        {
                            foreach (var p in config.Proxies)
                            {
                                Proxies.Add(new ProxyViewModel(p));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($"读取配置失败: {ex.Message}");
                }
            }

            if (Proxies.Count > 0)
            {
                SelectedProxy = Proxies[0];
            }
        }

        private void PersistConfig()
        {
            var config = new Configuration
            {
                Proxies = Proxies.Select(p => p.ToModel()).ToList()
            };

            try
            {
                string json = JsonConvert.SerializeObject(config, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                File.WriteAllText(ConfigPath, json);
                AppendLog("配置文件已保存");
            }
            catch (Exception ex)
            {
                AppendLog($"保存配置失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private void AddProxy()
        {
            var newProxy = new ProxyViewModel(new Configuration.Proxy
            {
                Socks5ProxyEndpoint = "127.0.0.1:10808",
                AppNames = new System.Collections.Generic.List<string>(),
                SupportedProtocols = new System.Collections.Generic.List<string> { "TCP", "UDP" }
            });
            Proxies.Add(newProxy);
            SelectedProxy = newProxy;
        }

        [RelayCommand]
        private void RemoveProxy()
        {
            if (SelectedProxy != null)
            {
                Proxies.Remove(SelectedProxy);
                SelectedProxy = Proxies.FirstOrDefault();
            }
        }

        [RelayCommand]
        private void AddApp()
        {
            if (SelectedProxy != null)
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*",
                    Title = "选择需要代理的应用程序"
                };

                if (dialog.ShowDialog() == true)
                {
                    string fileName = Path.GetFileName(dialog.FileName);
                    if (!SelectedProxy.AppNames.Contains(fileName))
                    {
                        SelectedProxy.AppNames.Add(fileName);
                    }
                }
            }
        }

        [RelayCommand]
        private void AddManualApp()
        {
            if (SelectedProxy != null && !string.IsNullOrWhiteSpace(ManualAppInput))
            {
                // Extract just the file name if a full path is pasted
                string fileName = Path.GetFileName(ManualAppInput.Trim());
                if (!fileName.ToLower().EndsWith(".exe"))
                {
                    fileName += ".exe";
                }

                if (!SelectedProxy.AppNames.Contains(fileName))
                {
                    SelectedProxy.AppNames.Add(fileName);
                }
                ManualAppInput = string.Empty; // Clear input after adding
            }
        }

        [RelayCommand]
        private void RemoveApp(string appName)
        {
            if (SelectedProxy != null && !string.IsNullOrEmpty(appName))
            {
                SelectedProxy.AppNames.Remove(appName);
            }
        }

        [RelayCommand]
        private void ToggleStart()
        {
            if (IsRunning)
            {
                StopProcess();
            }
            else
            {
                StartProcess();
            }
        }

        private void StartProcess()
        {
            PersistConfig();

            if (!File.Exists(ProgramPath))
            {
                AppendLog($"找不到核心程序: {Constants.ProgramName}，请将其与本程序放在同一目录！");
                return;
            }

            try
            {
                if (proxifyreProcess != null && !proxifyreProcess.HasExited)
                {
                    StopProcessInternal(false);
                }
            }
            catch { }

            AppendLog("正在启动 ProxiFyre...");

            proxifyreProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ProgramPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            proxifyreProcess.OutputDataReceived += processOutputHandler;
            proxifyreProcess.ErrorDataReceived += processErrorHandler;
            proxifyreProcess.Exited += processExitedHandler;

            try
            {
                proxifyreProcess.Start();
                
                processTracker.AddProcess(proxifyreProcess);
                
                proxifyreProcess.BeginOutputReadLine();
                proxifyreProcess.BeginErrorReadLine();
                IsRunning = true;
            }
            catch (Exception ex)
            {
                AppendLog($"启动失败: {ex.Message}");
                IsRunning = false;
            }
        }

        private void StopProcess()
        {
            StopProcessInternal(true);
        }

        private void StopProcessInternal(bool writeLog)
        {
            var process = proxifyreProcess;
            if (process == null)
            {
                IsRunning = false;
                return;
            }

            try
            {
                if (writeLog)
                {
                    AppendLog("正在停止 ProxiFyre...");
                }

                process.OutputDataReceived -= processOutputHandler;
                process.ErrorDataReceived -= processErrorHandler;
                process.Exited -= processExitedHandler;

                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(3000);
                }
            }
            catch (Exception ex)
            {
                if (writeLog)
                {
                    AppendLog($"停止进程时出错: {ex.Message}");
                }
            }
            finally
            {
                process.Dispose();
                if (ReferenceEquals(proxifyreProcess, process))
                {
                    proxifyreProcess = null;
                }
                IsRunning = false;
            }
        }

        private void OnProcessOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                AppendLog(e.Data);
            }
        }

        private void OnProcessErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                AppendLog(e.Data);
            }
        }

        private void OnProcessExited(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.InvokeAsync(() => IsRunning = false);
            AppendLog("ProxiFyre 已停止。");
        }

        private void FlushPendingLogs(object sender, EventArgs e)
        {
            int flushed = 0;
            while (flushed < 200 && pendingLogLines.TryDequeue(out var line))
            {
                LogLines.Add(line);
                flushed++;
            }

            if (LogLines.Count > MaxLogLines)
            {
                int removeCount = LogLines.Count - MaxLogLines;
                for (int i = 0; i < removeCount; i++)
                {
                    LogLines.RemoveAt(0);
                }
            }
        }

        private void AppendLog(string message)
        {
            pendingLogLines.Enqueue($"{DateTime.Now:HH:mm:ss} - {message}");
        }

        [RelayCommand]
        private void ExitApp()
        {
            logFlushTimer.Stop();
            StopProcess();
            processTracker.Dispose();
            Application.Current.Shutdown();
        }
    }

    public partial class ProxyViewModel : ObservableObject
    {
        [ObservableProperty]
        private string ip = "127.0.0.1";

        [ObservableProperty]
        private string port = "10808";

        [ObservableProperty]
        private string username;

        [ObservableProperty]
        private string password;

        [ObservableProperty]
        private bool isTcp;

        [ObservableProperty]
        private bool isUdp;

        [ObservableProperty]
        private ObservableCollection<string> appNames = new ObservableCollection<string>();

        public ProxyViewModel(Configuration.Proxy proxy)
        {
            if (!string.IsNullOrEmpty(proxy.Socks5ProxyEndpoint) && proxy.Socks5ProxyEndpoint.Contains(":"))
            {
                var parts = proxy.Socks5ProxyEndpoint.Split(':');
                Ip = parts[0];
                if (parts.Length > 1) Port = parts[1];
            }

            Username = proxy.Username;
            Password = proxy.Password;

            if (proxy.SupportedProtocols != null)
            {
                IsTcp = proxy.SupportedProtocols.Contains("TCP");
                IsUdp = proxy.SupportedProtocols.Contains("UDP");
            }

            if (proxy.AppNames != null)
            {
                foreach (var app in proxy.AppNames)
                {
                    AppNames.Add(app);
                }
            }
        }

        public Configuration.Proxy ToModel()
        {
            var protocols = new System.Collections.Generic.List<string>();
            if (IsTcp) protocols.Add("TCP");
            if (IsUdp) protocols.Add("UDP");

            return new Configuration.Proxy
            {
                Socks5ProxyEndpoint = $"{Ip}:{Port}",
                Username = Username,
                Password = Password,
                SupportedProtocols = protocols,
                AppNames = AppNames.ToList()
            };
        }

        public string DisplayName => $"{Ip}:{Port}";
        
        protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.PropertyName == nameof(Ip) || e.PropertyName == nameof(Port))
            {
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }
}
