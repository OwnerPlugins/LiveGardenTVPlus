using System.Windows;

namespace LiveGardenTVPlus.Services
{
    public static class ThemeManager
    {
        public static void SetTheme(string themeName)
        {
            var uri = new Uri($"Themes/{themeName}.xaml", UriKind.Relative);
            var dict = new ResourceDictionary { Source = uri };
            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(dict);
        }
    }
}