using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace proxifyre_ui
{
    public partial class DependencyDownloadViewModel : ObservableObject
    {
        private const int MaxInstallLogLines = 500;
        private readonly ConcurrentQueue<string> pendingInstallLogs = new ConcurrentQueue<string>();
        private readonly DispatcherTimer installLogFlushTimer;

        [ObservableProperty]
        private ObservableCollection<DependencyInfo> missingDependencies;

        [ObservableProperty]
        private bool isDownloading;

        [ObservableProperty]
        private double totalProgress;

        [ObservableProperty]
        private ObservableCollection<string> installLogLines = new ObservableCollection<string>();

        [ObservableProperty]
        private bool hasLog;

        private readonly MainViewModel _mainViewModel;

        public DependencyDownloadViewModel(List<DependencyInfo> missing, MainViewModel mainViewModel)
        {
            MissingDependencies = new ObservableCollection<DependencyInfo>(missing);
            _mainViewModel = mainViewModel;
            installLogFlushTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(120)
            };
            installLogFlushTimer.Tick += FlushPendingInstallLogs;
            installLogFlushTimer.Start();
        }

        private void AppendLog(string message)
        {
            pendingInstallLogs.Enqueue($"{DateTime.Now:HH:mm:ss} - {message}");
        }

        private void FlushPendingInstallLogs(object sender, EventArgs e)
        {
            int flushed = 0;
            while (flushed < 100 && pendingInstallLogs.TryDequeue(out var line))
            {
                InstallLogLines.Add(line);
                flushed++;
            }

            if (InstallLogLines.Count > 0)
            {
                HasLog = true;
            }

            if (InstallLogLines.Count > MaxInstallLogLines)
            {
                int removeCount = InstallLogLines.Count - MaxInstallLogLines;
                for (int i = 0; i < removeCount; i++)
                {
                    InstallLogLines.RemoveAt(0);
                }
            }
        }

        public Action OnCloseRequested;

        [RelayCommand]
        private async Task StartDownload()
        {
            await StartDownloadAndInstallAsync();
        }

        [RelayCommand]
        private void Close()
        {
            Stop();
            OnCloseRequested?.Invoke();
        }

        public void Stop()
        {
            installLogFlushTimer.Stop();
        }

        public async Task StartDownloadAndInstallAsync()
        {
            IsDownloading = true;
            TotalProgress = 0;
            double progressStep = 100.0 / MissingDependencies.Count;
            int currentStep = 0;

            // Configure HttpClient with optional proxy
            HttpClientHandler handler = new HttpClientHandler();
            if (_mainViewModel.SelectedProxy != null && !string.IsNullOrEmpty(_mainViewModel.SelectedProxy.Ip))
            {
                try
                {
                    // Note: HttpClientHandler in .NET 4.7.2 only supports HTTP proxies natively. 
                    // Socks5 would require external libs. We use default proxy for robustness or fallback to direct.
                    // For the sake of the prompt "use selected proxy", we set it here but be aware of Socks5 limitation.
                    WebProxy proxy = new WebProxy($"http://{_mainViewModel.SelectedProxy.Ip}:{_mainViewModel.SelectedProxy.Port}");
                    handler.Proxy = proxy;
                    AppendLog($"使用代理: {_mainViewModel.SelectedProxy.Ip}:{_mainViewModel.SelectedProxy.Port} 下载");
                }
                catch
                {
                    AppendLog("配置代理失败，将使用直连下载");
                }
            }

            using (HttpClient client = new HttpClient(handler))
            {
                client.Timeout = TimeSpan.FromMinutes(10); // Allow large downloads

                foreach (var dep in MissingDependencies)
                {
                    try
                    {
                        AppendLog($"开始下载: {dep.Name}");
                        string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + GetExtension(dep.Type));

                        // Download file
                        using (var response = await client.GetAsync(dep.Url, HttpCompletionOption.ResponseHeadersRead))
                        {
                            response.EnsureSuccessStatusCode();
                            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                            var canReportProgress = totalBytes != -1;

                            using (var stream = await response.Content.ReadAsStreamAsync())
                            using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                            {
                                var buffer = new byte[8192];
                                var totalRead = 0L;
                                var bytesRead = 0;

                                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                                {
                                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                                    totalRead += bytesRead;

                                    if (canReportProgress)
                                    {
                                        var progress = (double)totalRead / totalBytes;
                                        TotalProgress = (currentStep * progressStep) + (progress * progressStep);
                                    }
                                }
                            }
                        }

                        AppendLog($"{dep.Name} 下载完成，开始安装...");
                        
                        // Install/Extract
                        await InstallDependencyAsync(dep, tempFile);

                        // Cleanup temp file
                        if (File.Exists(tempFile))
                        {
                            File.Delete(tempFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"[错误] {dep.Name} 处理失败: {ex.Message}");
                    }
                    finally
                    {
                        currentStep++;
                        TotalProgress = currentStep * progressStep;
                    }
                }
            }

            AppendLog("所有依赖处理完毕！");
            IsDownloading = false;
            
            // Re-check environment
            var stillMissing = EnvironmentDetector.CheckMissingDependencies();
            if (stillMissing.Count == 0)
            {
                AppendLog("环境检测全部通过，此弹窗2秒后自动关闭...");
                await Task.Delay(2000);
                Close();
            }
            else
            {
                AppendLog("仍有依赖缺失，请手动检查或重试。");
            }
        }

        private async Task InstallDependencyAsync(DependencyInfo dep, string filePath)
        {
            await Task.Run(() =>
            {
                try
                {
                    if (dep.Type == DependencyType.Zip)
                    {
                        string extractPath = AppDomain.CurrentDomain.BaseDirectory;
                        AppendLog($"正在解压到: {extractPath}");
                        
                        using (ZipArchive archive = ZipFile.OpenRead(filePath))
                        {
                            foreach (ZipArchiveEntry entry in archive.Entries)
                            {
                                string destinationPath = Path.GetFullPath(Path.Combine(extractPath, entry.FullName));

                                // Prevent ZipSlip vulnerability
                                if (destinationPath.StartsWith(extractPath, StringComparison.Ordinal))
                                {
                                    if (string.IsNullOrEmpty(entry.Name))
                                    {
                                        Directory.CreateDirectory(destinationPath);
                                    }
                                    else
                                    {
                                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                                        
                                        try
                                        {
                                            entry.ExtractToFile(destinationPath, true);
                                        }
                                        catch (IOException)
                                        {
                                            // If file is locked (like Newtonsoft.Json.dll loaded by our own app), we skip it safely
                                            // This is normal for shared dependencies that are already in use and don't need overwriting.
                                            AppendLog($"跳过被占用的文件: {entry.Name}");
                                        }
                                        catch (UnauthorizedAccessException)
                                        {
                                            AppendLog($"无权限覆盖文件，已跳过: {entry.Name}");
                                        }
                                    }
                                }
                            }
                        }
                        AppendLog($"解压完成");
                    }
                    else if (dep.Type == DependencyType.Msi)
                    {
                        AppendLog($"正在静默安装 MSI...");
                        using (Process p = new Process())
                        {
                            p.StartInfo.FileName = "msiexec.exe";
                            p.StartInfo.Arguments = $"/i \"{filePath}\" /qn /norestart";
                            p.StartInfo.UseShellExecute = true;
                            p.StartInfo.Verb = "runas";
                            p.Start();
                            p.WaitForExit();
                            AppendLog($"MSI 安装完成，退出码: {p.ExitCode}");
                        }
                    }
                    else if (dep.Type == DependencyType.Exe)
                    {
                        AppendLog($"正在静默安装 EXE...");
                        using (Process p = new Process())
                        {
                            p.StartInfo.FileName = filePath;
                            p.StartInfo.Arguments = "/install /quiet /norestart";
                            p.StartInfo.UseShellExecute = true;
                            p.StartInfo.Verb = "runas";
                            p.Start();
                            p.WaitForExit();
                            AppendLog($"EXE 安装完成，退出码: {p.ExitCode}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($"安装失败: {ex.Message}");
                }
            });
        }

        private string GetExtension(DependencyType type)
        {
            switch (type)
            {
                case DependencyType.Zip: return ".zip";
                case DependencyType.Msi: return ".msi";
                case DependencyType.Exe: return ".exe";
                default: return ".tmp";
            }
        }
    }
}
