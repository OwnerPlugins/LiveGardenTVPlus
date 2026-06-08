using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using LiveGardenTVPlus.Services;

namespace LiveGardenTVPlus.Views
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            ApplyLanguage();
            _ = LoadChangelogAsync();
        }

        private void ApplyLanguage()
        {
            Title = LanguageManager.GetTranslation("About") + " - TVGarden+";
            VersionText.Text = $"{LanguageManager.GetTranslation("Version")} {GetVersion()}";
            SupportButton.Content = LanguageManager.GetTranslation("Support");
        }

        private string GetVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }

        private async Task LoadChangelogAsync()
        {
            string changelog = await FetchChangelogFromReadmeAsync();
            if (string.IsNullOrEmpty(changelog))
            {
                changelog = @"✨ Key Features

• Full EPG support with timezone handling
• Channel logos (tvg‑logo)
• M3U8 / HLS playback
• Resizable sidebar
• Dynamic language switching
• Playlist editor (edit channels, groups, favorites, epg, logos)
• URL health check and export
• Timeshift (pause live streams)
• Import/Export favorites to M3U/json
• Advanced List Editor
• Auto‑updater
• Auto-assign logos";
            }
            ChangelogText.Text = changelog;
        }

        private async Task<string> FetchChangelogFromReadmeAsync()
        {
            const string readmeUrl = "https://raw.githubusercontent.com/OwnerPlugins/LiveGardenTVPlus/refs/heads/main/README.md";
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                string readme = await client.GetStringAsync(readmeUrl);
                return ExtractChangelog(readme);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error fetching README: {ex.Message}");
                return null;
            }
        }

        private string ExtractChangelog(string readme)
        {
            var match = Regex.Match(readme, @"## Changelog(.*?)(?=\n## Getting started|\z)", RegexOptions.Singleline);
            if (!match.Success) return null;
            string changelog = match.Groups[1].Value.Trim();
            changelog = Regex.Replace(changelog, @"!\[.*?\]\(.*?\)", "");
            return changelog;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void GitHubButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/OwnerPlugins/LiveGardenTVPlus",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cannot open browser: {ex.Message}");
            }
        }

        private void SupportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://www.corvoboys.org",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cannot open browser: {ex.Message}");
            }
        }
    }
}