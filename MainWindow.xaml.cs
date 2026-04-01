using System;
using System.ComponentModel;
using System.Windows;
using Wpf.Ui.Controls;

namespace proxifyre_ui
{
    public partial class MainWindow : FluentWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);
            Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this);

            if (this.DataContext is MainViewModel vm)
            {
                vm.LogLines.CollectionChanged += (s, e) =>
                {
                    if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                    {
                        var newItem = e.NewItems[0];
                        LogListBox.ScrollIntoView(newItem);
                    }
                };
            }
        }

        private void FluentWindow_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private void TrayIcon_LeftClick(object sender, RoutedEventArgs e)
        {
            this.Show();
            this.Activate();
            if (this.WindowState == WindowState.Minimized)
            {
                this.WindowState = WindowState.Normal;
            }
        }

        private void TrayMenu_ExitApp_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainViewModel vm)
            {
                vm.ExitAppCommand.Execute(null);
            }
            else
            {
                Application.Current.Shutdown();
            }
        }
    }
}
