using LiveGardenTVPlus.Services;
using System.Windows;
using System.Windows.Controls;

namespace LiveGardenTVPlus.Views
{
    public partial class SettingsWindow : Window
    {
        public string SelectedPlaylistUrl { get; private set; }

        public SettingsWindow()
        {
            InitializeComponent();
            PopulateLanguageCombo();
            ApplyLanguage();
            LoadSettings();
            BufferSlider.ValueChanged += (s, e) => BufferValue.Text = $"{e.NewValue:F0} sec";
            _ = LoadPlaylistsFromGitHubAsync();
        }

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

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            var prefs = UserPreferences.Load();
            prefs.BufferSeconds = (int)BufferSlider.Value;
            if (LangCombo.SelectedItem is ComboBoxItem selectedLang)
                prefs.Language = selectedLang.Tag.ToString();
            if (PlaylistCombo.SelectedItem is ComboBoxItem selectedPlaylist && selectedPlaylist.Tag != null)
                prefs.PlaylistUrl = selectedPlaylist.Tag.ToString();
            prefs.Save();

            LanguageManager.LoadLanguage(prefs.Language);
            if (Application.Current.MainWindow is MainWindow main)
                main.ApplyLanguage();

            if (!string.IsNullOrEmpty(prefs.PlaylistUrl))
            {
                _ = (Application.Current.MainWindow as MainWindow)?.LoadPlaylistFromUrl(prefs.PlaylistUrl);
            }

            DialogResult = true;
            Close();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void PopulateLanguageCombo()
        {
            LangCombo.Items.Clear();
            foreach (var lang in LanguageManager.GetAvailableLanguages())
                LangCombo.Items.Add(new ComboBoxItem { Content = lang, Tag = lang });
        }

        private void ApplyLanguage()
        {
            LanguageLabel.Text = LanguageManager.GetTranslation("Language");
            BufferLabel.Text = LanguageManager.GetTranslation("Buffer (seconds)");
            OnlinePlaylistLabel.Text = LanguageManager.GetTranslation("Online Playlist");
            LoadPlaylistBtn.Content = LanguageManager.GetTranslation("LOAD");
            SaveBtn.Content = LanguageManager.GetTranslation("SAVE");
            CancelBtn.Content = LanguageManager.GetTranslation("CANCEL");
            RefreshPlaylistsBtn.Content = LanguageManager.GetTranslation("Refresh from GitHub");
            Title = LanguageManager.GetTranslation("Settings");
        }

        private void LoadSettings()
        {
            var prefs = UserPreferences.Load();
            BufferSlider.Value = prefs.BufferSeconds;
            BufferValue.Text = $"{prefs.BufferSeconds} sec";

            string savedLang = prefs.Language;
            foreach (ComboBoxItem item in LangCombo.Items)
            {
                if (item.Tag.ToString() == savedLang)
                {
                    LangCombo.SelectedItem = item;
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