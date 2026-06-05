using LiveGardenTVPlus.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LiveGardenTVPlus.Views
{
    public partial class ColorPickerWindow : Window
    {
        public string SelectedTheme { get; private set; } = "LightTheme";

        public ColorPickerWindow()
        {
            InitializeComponent();
            Title = LanguageManager.GetTranslation("Select Theme");
            var themes = new[]
            {
                "LightTheme", "BlueTheme", "GreenTheme","BrownTheme",
                "OrangeTheme", "PurpleTheme", "RedTheme", "TealTheme",
                "PinkTheme", "CyanTheme", "LimeTheme", "IndigoTheme"
            };
            foreach (var t in themes)
            {
                var btn = new Button
                {
                    Content = t.Replace("Theme", ""),
                    Width = 80,
                    Height = 40,
                    Margin = new Thickness(5),
                    Tag = t,
                    Background = GetBrushForTheme(t)
                };
                btn.Click += (s, e) => { SelectedTheme = (string)((Button)s).Tag; DialogResult = true; Close(); };
                ColorPanel.Children.Add(btn);
            }
        }

        private SolidColorBrush GetBrushForTheme(string theme)
        {
            switch (theme)
            {
                case "LightTheme": return Brushes.LightGray;
                case "DarkTheme": return Brushes.DimGray;
                case "BlueTheme": return Brushes.LightBlue;
                case "GreenTheme": return Brushes.LightGreen;
                case "OrangeTheme": return Brushes.Orange;
                case "PurpleTheme": return Brushes.Purple;
                case "RedTheme": return Brushes.Red;
                case "TealTheme": return Brushes.Teal;
                default: return Brushes.White;
            }
        }
    }
}