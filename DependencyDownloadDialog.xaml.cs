using System;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace proxifyre_ui
{
    public partial class DependencyDownloadDialog : ContentDialog
    {
        public DependencyDownloadDialog(ContentPresenter dialogPresenter) : base(dialogPresenter)
        {
            InitializeComponent();
            
            // Auto scroll to bottom for logs
            var textBox = (System.Windows.Controls.TextBox)this.FindName("LogTextBox");
            if (textBox != null)
            {
                textBox.TextChanged += (s, e) => textBox.ScrollToEnd();
            }
        }
    }
}