using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LiveGardenTVPlus.Services;
using LiveGardenTVPlus.Models;

namespace LiveGardenTVPlus.Views
{
    public partial class SettingsWindow : Window
    {
        public string SelectedPlaylistUrl { get; private set; }
        private List<KeyValuePair<string, string>> _epgSources = new List<KeyValuePair<string, string>>();

        public SettingsWindow()
        {
            InitializeComponent();
            PopulateLanguageCombo();
            ApplyLanguage();
            LoadSettings();
            BufferSlider.ValueChanged += (s, e) => BufferValue.Text = $"{e.NewValue:F0} sec";
            this.Loaded += (s, e) => LoadThresholds();
            _ = LoadPlaylistsFromGitHubAsync();
            _ = LoadEpgSources();
        }

        // ------------------------------------------------------------------
        // Language & UI
        // ------------------------------------------------------------------
        private void ApplyLanguage()
        {
            LanguageLabel.Text = LanguageManager.GetTranslation("Language");
            BufferLabel.Text = LanguageManager.GetTranslation("Buffer (seconds)");
            OnlinePlaylistLabel.Text = LanguageManager.GetTranslation("Online Playlist");
            RefreshPlaylistsBtn.Content = LanguageManager.GetTranslation("Refresh from GitHub");
            LoadPlaylistBtn.Content = LanguageManager.GetTranslation("LOAD");
            EpgLabel.Text = LanguageManager.GetTranslation("EPG Source");
            RefreshEpgBtn.Content = LanguageManager.GetTranslation("Refresh EPG List");
            LogosLabel.Text = LanguageManager.GetTranslation("Logos Source");
            LogosSubFolderLabel.Text = LanguageManager.GetTranslation("Logos Subfolder");
            SaveBtn.Content = LanguageManager.GetTranslation("SAVE");
            CancelBtn.Content = LanguageManager.GetTranslation("CANCEL");
            Title = LanguageManager.GetTranslation("Settings");

            // Threshold labels (ensure they have x:Name in XAML)
            var epgThresholdLabel = FindName("EpgThresholdLabel") as TextBlock;
            if (epgThresholdLabel != null)
                epgThresholdLabel.Text = LanguageManager.GetTranslation("EPG Matching Threshold");

            var logosThresholdLabel = FindName("LogosThresholdLabel") as TextBlock;
            if (logosThresholdLabel != null)
                logosThresholdLabel.Text = LanguageManager.GetTranslation("Logos Matching Threshold");
        }

        // ------------------------------------------------------------------
        // EPG Sources
        // ------------------------------------------------------------------
        private async Task LoadEpgSources()
        {
            try
            {
                RefreshEpgBtn.IsEnabled = false;
                RefreshEpgBtn.Content = "Loading...";
                string baseUrl = "https://epgshare01.online/epgshare01/";
                using var client = new HttpClient();
                string html = await client.GetStringAsync(baseUrl);
                var matches = Regex.Matches(html, "<a href=\"([^\"]+\\.xml\\.gz)\"");
                _epgSources.Clear();
                foreach (Match m in matches)
                {
                    string fileName = m.Groups[1].Value;
                    string fullUrl = baseUrl + fileName;
                    string displayName = fileName.Replace("epg_ripper_", "").Replace(".xml.gz", "").ToUpper();
                    _epgSources.Add(new KeyValuePair<string, string>(displayName, fullUrl));
                }
                EpgCombo.ItemsSource = _epgSources;
                EpgCombo.DisplayMemberPath = "Key";
                EpgCombo.SelectedValuePath = "Value";

                var prefs = UserPreferences.Load();
                if (!string.IsNullOrEmpty(prefs.EpgUrl))
                {
                    var existing = _epgSources.Find(x => x.Value == prefs.EpgUrl);
                    if (existing.Key != null)
                        EpgCombo.SelectedItem = existing;
                    else
                        EpgCombo.Text = prefs.EpgUrl;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading EPG list: {ex.Message}");
            }
            finally
            {
                RefreshEpgBtn.IsEnabled = true;
                RefreshEpgBtn.Content = "Refresh EPG List";
            }
        }

        private async void RefreshEpgBtn_Click(object sender, RoutedEventArgs e)
        {
            await LoadEpgSources();
        }

        // ------------------------------------------------------------------
        // GitHub Playlists
        // ------------------------------------------------------------------
        private async Task LoadPlaylistsFromGitHubAsync()
        {
            try
            {
                RefreshPlaylistsBtn.IsEnabled = false;
                RefreshPlaylistsBtn.Content = LanguageManager.GetTranslation("Loading...");

                var playlists = await GitHubPlaylistFetcher.GetM3uPlaylistsAsync();

                PlaylistCombo.Items.Clear();

                if (playlists == null || playlists.Count == 0)
                {
                    PlaylistCombo.Items.Add(new ComboBoxItem { Content = LanguageManager.GetTranslation("No playlist found"), Tag = "" });
                    PlaylistCombo.SelectedIndex = 0;
                    MessageBox.Show(LanguageManager.GetTranslation("No .m3u files found on GitHub repository."),
                                    LanguageManager.GetTranslation("Info"), MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                foreach (var pl in playlists)
                {
                    PlaylistCombo.Items.Add(new ComboBoxItem
                    {
                        Content = pl.DisplayName,
                        Tag = pl.RawUrl
                    });
                }

                var prefs = UserPreferences.Load();
                if (!string.IsNullOrEmpty(prefs.PlaylistUrl))
                {
                    bool found = false;
                    foreach (ComboBoxItem item in PlaylistCombo.Items)
                    {
                        if (item.Tag != null && item.Tag.ToString() == prefs.PlaylistUrl)
                        {
                            PlaylistCombo.SelectedItem = item;
                            found = true;
                            break;
                        }
                    }
                    if (!found && PlaylistCombo.Items.Count > 0)
                        PlaylistCombo.SelectedIndex = 0;
                }
                else
                {
                    if (PlaylistCombo.Items.Count > 0)
                        PlaylistCombo.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LanguageManager.GetTranslation("Error loading playlists: {0}"), ex.Message),
                                LanguageManager.GetTranslation("GitHub Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                PlaylistCombo.Items.Clear();
                PlaylistCombo.Items.Add(new ComboBoxItem { Content = LanguageManager.GetTranslation("Error loading"), Tag = "" });
                PlaylistCombo.SelectedIndex = 0;
            }
            finally
            {
                RefreshPlaylistsBtn.IsEnabled = true;
                RefreshPlaylistsBtn.Content = LanguageManager.GetTranslation("Refresh from GitHub");
            }
        }

        private async void RefreshPlaylistsBtn_Click(object sender, RoutedEventArgs e)
        {
            await LoadPlaylistsFromGitHubAsync();
        }

        private void LoadPlaylistBtn_Click(object sender, RoutedEventArgs e)
        {
            if (PlaylistCombo.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag != null)
            {
                string url = selectedItem.Tag.ToString();
                if (!string.IsNullOrEmpty(url))
                {
                    SelectedPlaylistUrl = url;
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show(LanguageManager.GetTranslation("Invalid playlist URL."),
                                    LanguageManager.GetTranslation("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show(LanguageManager.GetTranslation("Select a playlist first."),
                                LanguageManager.GetTranslation("Info"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // ------------------------------------------------------------------
        // Threshold sliders
        // ------------------------------------------------------------------
        private void EpgThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (EpgThresholdValue != null)
                EpgThresholdValue.Text = e.NewValue.ToString("F2");
        }

        private void LogoThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (LogoThresholdValue != null)
                LogoThresholdValue.Text = e.NewValue.ToString("F2");
        }

        private void LoadThresholds()
        {
            var prefs = UserPreferences.Load();
            LogoThresholdSlider.Value = prefs.LogoMatchingThreshold;
            EpgThresholdSlider.Value = prefs.EpgMatchingThreshold;
        }

        // ------------------------------------------------------------------
        // Save & Cancel
        // ------------------------------------------------------------------
        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            var prefs = UserPreferences.Load();

            // Buffer
            prefs.BufferSeconds = (int)BufferSlider.Value;

            // Language
            if (LangCombo.SelectedItem is ComboBoxItem selectedLang)
            {
                prefs.Language = selectedLang.Tag.ToString();
            }

            // Playlist URL (salvata, ma non caricata automaticamente)
            if (PlaylistCombo.SelectedItem is ComboBoxItem selectedPlaylist && selectedPlaylist.Tag != null)
            {
                prefs.PlaylistUrl = selectedPlaylist.Tag.ToString();
            }

            // Matching thresholds
            prefs.LogoMatchingThreshold = LogoThresholdSlider.Value;
            prefs.EpgMatchingThreshold = EpgThresholdSlider.Value;

            // EPG URL
            if (EpgCombo.SelectedItem is KeyValuePair<string, string> selectedEpg)
                prefs.EpgUrl = selectedEpg.Value;
            else if (!string.IsNullOrEmpty(EpgCombo.Text))
                prefs.EpgUrl = EpgCombo.Text;

            // Logos repository source
            if (LogosCombo.SelectedItem is ComboBoxItem selectedLogoSource && selectedLogoSource.Tag != null)
            {
                string[] parts = selectedLogoSource.Tag.ToString().Split('|');
                if (parts.Length == 3)
                {
                    prefs.LogosRepositoryOwner = parts[0];
                    prefs.LogosRepositoryRepo = parts[1];
                    prefs.LogosRepositoryPath = parts[2];
                }
            }

            // Logos subfolder
            if (LogosSubFolderCombo.SelectedItem is ComboBoxItem selectedSubFolder && selectedSubFolder.Tag != null)
            {
                prefs.LogosSubFolder = selectedSubFolder.Tag.ToString();
            }

            prefs.Save();

            // Apply language change globally
            LanguageManager.LoadLanguage(prefs.Language);

            // Update main window UI only (do not load playlist automatically)
            if (Application.Current.MainWindow is MainWindow main)
            {
                main.ApplyLanguage();
                // Remove the automatic playlist loading from here
                // _ = main.LoadPlaylistFromUrl(...);   // <-- DELETE THIS LINE
            }

            DialogResult = true;
            Close();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // ------------------------------------------------------------------
        // Language combo & settings loading
        // ------------------------------------------------------------------
        private void PopulateLanguageCombo()
        {
            LangCombo.Items.Clear();
            foreach (var lang in LanguageManager.GetAvailableLanguages())
                LangCombo.Items.Add(new ComboBoxItem { Content = lang, Tag = lang });
        }

        private void LoadSettings()
        {
            var prefs = UserPreferences.Load();

            // Thresholds
            LogoThresholdSlider.Value = prefs.LogoMatchingThreshold;
            LogoThresholdValue.Text = prefs.LogoMatchingThreshold.ToString("F2");
            EpgThresholdSlider.Value = prefs.EpgMatchingThreshold;
            EpgThresholdValue.Text = prefs.EpgMatchingThreshold.ToString("F2");

            // Buffer
            BufferSlider.Value = prefs.BufferSeconds;
            BufferValue.Text = $"{prefs.BufferSeconds} sec";

            // Language
            string savedLang = prefs.Language;
            foreach (ComboBoxItem item in LangCombo.Items)
            {
                if (item.Tag.ToString() == savedLang)
                {
                    LangCombo.SelectedItem = item;
                    break;
                }
            }

            // Logos repository
            string savedLogoKey = $"{prefs.LogosRepositoryOwner}|{prefs.LogosRepositoryRepo}|{prefs.LogosRepositoryPath}";
            foreach (ComboBoxItem item in LogosCombo.Items)
            {
                if (item.Tag != null && item.Tag.ToString() == savedLogoKey)
                {
                    LogosCombo.SelectedItem = item;
                    break;
                }
            }

            // Logos subfolder
            foreach (ComboBoxItem item in LogosSubFolderCombo.Items)
            {
                if (item.Tag != null && item.Tag.ToString() == prefs.LogosSubFolder)
                {
                    LogosSubFolderCombo.SelectedItem = item;
                    break;
                }
            }
        }

        private void LangCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LangCombo.SelectedItem is ComboBoxItem selected)
            {
                string langName = selected.Tag.ToString();
                LanguageManager.LoadLanguage(langName);
                ApplyLanguage();
                if (Application.Current.MainWindow is MainWindow main)
                    main.ApplyLanguage();
            }
        }
    }
}
