using System;
using System.Windows;

namespace StdfViewer
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var app = new Application();
            var window = new ViewerWindow();
            if (args.Length > 0)
                window.QueueLoad(args[0]);
            app.Run(window);
        }
    }
}
