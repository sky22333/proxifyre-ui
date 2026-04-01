using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace proxifyre_ui
{
    public partial class App : Application
    {
        // Unique GUID for this application to use as a global Mutex name
        private const string MutexName = "Global\\proxifyre_ui_SingleInstanceMutex_7A2B4C1D";
        private Mutex _mutex;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Try to grab the mutex
            _mutex = new Mutex(true, MutexName, out bool createdNew);

            if (!createdNew)
            {
                // Another instance is already running.
                // Try to find its process and bring its window to the foreground.
                Process currentProcess = Process.GetCurrentProcess();
                Process existingProcess = Process.GetProcessesByName(currentProcess.ProcessName)
                                                 .FirstOrDefault(p => p.Id != currentProcess.Id);

                if (existingProcess != null && existingProcess.MainWindowHandle != IntPtr.Zero)
                {
                    ShowWindow(existingProcess.MainWindowHandle, SW_RESTORE);
                    SetForegroundWindow(existingProcess.MainWindowHandle);
                }

                // Shutdown this duplicate instance immediately
                Application.Current.Shutdown();
                return;
            }

            base.OnStartup(e);

            var mainWindow = new MainWindow();
            
            // Check if it's started by Windows startup registry
            // We can pass a command line argument or just hide by default if needed
            // For now, we show the main window if it's not silent
            bool isSilent = false;
            foreach (string arg in e.Args)
            {
                if (arg.ToLower() == "-silent" || arg.ToLower() == "/silent")
                {
                    isSilent = true;
                }
            }

            if (!isSilent)
            {
                mainWindow.Show();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Release the mutex when the app naturally exits
            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
            }
            base.OnExit(e);
        }
    }
}
