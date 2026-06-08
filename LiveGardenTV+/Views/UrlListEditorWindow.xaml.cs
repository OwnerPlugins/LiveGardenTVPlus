using System.Collections.Generic;
using System.Linq;
using System.Windows;
using LiveGardenTVPlus.Services;

namespace LiveGardenTVPlus.Views
{
    public partial class UrlListEditorWindow : Window
    {
        public List<string> Urls { get; private set; }

        public UrlListEditorWindow(List<string> urls)
        {
            InitializeComponent();
            Urls = new List<string>(urls ?? new List<string>());
            UrlsTextBox.Text = string.Join("\n", Urls);
            LanguageManager.LanguageChanged += ApplyLanguage;
            ApplyLanguage();
        }

        private void ApplyLanguage()
        {
            Title = LanguageManager.GetTranslation("Edit URLs");
            LabelText.Text = LanguageManager.GetTranslation("URL list (one per line)");
            OkBtn.Content = LanguageManager.GetTranslation("OK");
            CancelBtn.Content = LanguageManager.GetTranslation("Cancel");
        }

        private void OkBtn_Click(object sender, RoutedEventArgs e)
        {
            Urls = UrlsTextBox.Text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries)
                                   .Select(u => u.Trim())
                                   .Where(u => !string.IsNullOrEmpty(u))
                                   .ToList();
            DialogResult = true;
            Close();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}