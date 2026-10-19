using System.Windows;

namespace StdfViewer
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var window = new ViewerWindow();
            if (e.Args.Length > 0)
                window.QueueLoad(e.Args[0]);
            MainWindow = window;
            window.Show();
        }
    }
}
