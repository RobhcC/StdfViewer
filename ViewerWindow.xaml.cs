using System;
using System.Windows;
using StdfViewer.ViewModels;

namespace StdfViewer
{
    public sealed partial class ViewerWindow : Window
    {
        public ViewerWindow()
        {
            InitializeComponent();
        }

        protected override void OnClosed(EventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null)
                viewModel.Dispose();
            base.OnClosed(e);
        }
    }
}
