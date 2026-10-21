using System.Windows;
using StdfViewer.Services;
using StdfViewer.ViewModels;

namespace StdfViewer
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Loc.Initialize();

            var store = new HistoryStore();
            store.Load();
            var main = new MainViewModel(store, new DialogService());
            var window = new ViewerWindow { DataContext = main };
            MainWindow = window;
            window.Show();

            if (e.Args.Length > 0)
                _ = main.LoadAsync(e.Args[0]);
        }
    }
}
