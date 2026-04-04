using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace proxifyre_ui
{
    public partial class MainWindow : FluentWindow
    {
        private bool logScrollPending;

        public MainWindow()
        {
            InitializeComponent();
            
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);
            Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this);

            if (this.DataContext is MainViewModel vm)
            {
                vm.LogLines.CollectionChanged += OnLogLinesCollectionChanged;
            }
        }

        private void OnLogLinesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add || LogListBox.Items.Count == 0 || logScrollPending)
            {
                return;
            }

            logScrollPending = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var lastItem = LogListBox.Items[LogListBox.Items.Count - 1];
                LogListBox.ScrollIntoView(lastItem);
                logScrollPending = false;
            }), DispatcherPriority.Background);
        }

        private void FluentWindow_Closing(object sender, CancelEventArgs e)
        {
            AppNameHintPopup.IsOpen = false;
            e.Cancel = true;
            this.Hide();
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

        private void AppNameHintButton_Click(object sender, RoutedEventArgs e)
        {
            AppNameHintPopup.IsOpen = !AppNameHintPopup.IsOpen;
        }
    }
}
