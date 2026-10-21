using System.Windows;
using Microsoft.Win32;

namespace StdfViewer.Services
{
    public sealed class DialogService : IDialogService
    {
        public string PickStdfFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = Loc.T("Str.Dlg.OpenFilter"),
                Title = Loc.T("Str.Dlg.OpenTitle"),
            };
            Window owner = Application.Current == null ? null : Application.Current.MainWindow;
            bool? result = owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
            return result == true ? dialog.FileName : null;
        }

        public bool Confirm(string title, string message)
        {
            Window owner = Application.Current == null ? null : Application.Current.MainWindow;
            MessageBoxResult result = owner == null
                ? MessageBox.Show(message, title, MessageBoxButton.OKCancel, MessageBoxImage.Question)
                : MessageBox.Show(owner, message, title, MessageBoxButton.OKCancel, MessageBoxImage.Question);
            return result == MessageBoxResult.OK;
        }
    }
}
