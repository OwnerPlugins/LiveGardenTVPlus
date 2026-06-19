using System;
using System.Windows;
using System.Windows.Threading;
using LiveGardenTVPlus.Services;

namespace LiveGardenTVPlus
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Catch UI thread exceptions
            DispatcherUnhandledException += (s, args) =>
            {
                Logger.WriteException(args.Exception, "DispatcherUnhandledException");
                MessageBox.Show($"An error occurred. Log saved to:\n{Logger.LogPath}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true; // Prevent crash (optional)
            };

            // Catch background thread exceptions
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                    Logger.WriteException(ex, "UnhandledException");
            };

            // Optional: log startup
            Logger.Info("Application started.");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Logger.Info("Application exited.");
            base.OnExit(e);
        }
    }
}