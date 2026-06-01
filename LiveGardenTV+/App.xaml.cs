using LiveGardenTVPlus.Services;
using System.Windows;

namespace LiveGardenTVPlus
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var prefs = UserPreferences.Load();
            LanguageManager.LoadLanguage(prefs.Language);
            ThemeManager.SetTheme(prefs.Theme);
        }
    }
}