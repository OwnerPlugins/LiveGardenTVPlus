using LiveGardenTVPlus.Services;
using System.IO;
using System.Windows;

namespace LiveGardenTVPlus
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Carica il tema LightTheme.xaml
            try
            {
                var themePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Themes", "LightTheme.xaml");
                if (File.Exists(themePath))
                {
                    var themeDict = new ResourceDictionary();
                    themeDict.Source = new Uri(themePath, UriKind.Absolute);
                    Application.Current.Resources.MergedDictionaries.Add(themeDict);
                }
                else
                {
                    // Log di avviso
                    File.AppendAllText(@"C:\temp\theme_error.txt", $"LightTheme.xaml non trovato in: {themePath}\n");
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(@"C:\temp\theme_error.txt", $"Errore caricamento tema: {ex}\n");
            }


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