using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace proxifyre_ui
{
    public partial class DependencyDownloadDialog : ContentDialog
    {
        private bool scrollPending;

        public DependencyDownloadDialog(ContentPresenter dialogPresenter) : base(dialogPresenter)
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is DependencyDownloadViewModel oldVm)
            {
                oldVm.InstallLogLines.CollectionChanged -= OnInstallLogLinesCollectionChanged;
            }

            if (e.NewValue is DependencyDownloadViewModel newVm)
            {
                newVm.InstallLogLines.CollectionChanged += OnInstallLogLinesCollectionChanged;
            }
        }

        private void OnInstallLogLinesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add || LogListBox.Items.Count == 0 || scrollPending)
            {
                return;
            }

            scrollPending = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var lastItem = LogListBox.Items[LogListBox.Items.Count - 1];
                LogListBox.ScrollIntoView(lastItem);
                scrollPending = false;
            }), DispatcherPriority.Background);
        }
    }
}
