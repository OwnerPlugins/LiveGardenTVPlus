using LiveGardenTVPlus.Models;
using LiveGardenTVPlus.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace LiveGardenTVPlus.Views
{
    public partial class ChannelDetailsWindow : Window, INotifyPropertyChanged
    {
        private ObservableCollection<ChannelJson> _channels;
        private int _currentIndex;
        private ChannelJson _workingCopy;

        public string LogoUrlPreview
        {
            get => _workingCopy?.logo_url;
            set { if (_workingCopy != null) _workingCopy.logo_url = value; OnPropertyChanged(nameof(LogoUrlPreview)); }
        }

        public string CurrentIndexInfo => $"{_currentIndex + 1} of {_channels?.Count ?? 0}";

        public ChannelDetailsWindow(ObservableCollection<ChannelJson> channels, int startIndex)
        {
            InitializeComponent();
            DataContext = this;
            _channels = channels;
            _currentIndex = startIndex;
            LoadChannel(_currentIndex);
            LanguageManager.LanguageChanged += ApplyLanguage;
            ApplyLanguage();
        }

        private void ApplyLanguage()
        {
            Title = LanguageManager.GetTranslation("Channel Details");
            NameLabel.Text = LanguageManager.GetTranslation("Name");
            UrlLabel.Text = LanguageManager.GetTranslation("URL (primary)");
            GroupLabel.Text = LanguageManager.GetTranslation("Group");
            LogoLabel.Text = LanguageManager.GetTranslation("Logo URL");
            TvgIdLabel.Text = LanguageManager.GetTranslation("TvgId");
            CountryLabel.Text = LanguageManager.GetTranslation("Country");
            LanguagesLabel.Text = LanguageManager.GetTranslation("Languages (comma)");
            PreviewLabel.Text = LanguageManager.GetTranslation("Logo Preview");
            FavoriteBox.Content = LanguageManager.GetTranslation("Favorite");
            GeoBlockedBox.Content = LanguageManager.GetTranslation("GeoBlocked");
            PrevBtn.Content = LanguageManager.GetTranslation("◀ Previous");
            NextBtn.Content = LanguageManager.GetTranslation("Next ▶");
            SaveBtn.Content = LanguageManager.GetTranslation("Save");
            CancelBtn.Content = LanguageManager.GetTranslation("Cancel");
        }

        private void LoadChannel(int index)
        {
            if (_channels == null || index < 0 || index >= _channels.Count) return;
            var original = _channels[index];
            _workingCopy = CloneChannel(original);
            LogoUrlPreview = _workingCopy.logo_url;
            RefreshUI();
            OnPropertyChanged(nameof(CurrentIndexInfo));
            UpdateNavigationButtons();
        }

        private ChannelJson CloneChannel(ChannelJson source)
        {
            return new ChannelJson
            {
                name = source.name,
                stream_urls = new List<string>(source.stream_urls ?? new List<string>()),
                logo_url = source.logo_url,
                group = source.group,
                tvg_id = source.tvg_id,
                isFavorite = source.isFavorite,
                country = source.country,
                youtube_urls = new List<string>(source.youtube_urls ?? new List<string>()),
                nanoid = source.nanoid,
                languages = new List<string>(source.languages ?? new List<string>()),
                isGeoBlocked = source.isGeoBlocked,
                UrlStatus = source.UrlStatus
            };
        }

        private void RefreshUI()
        {
            NameBox.Text = _workingCopy.name;
            UrlBox.Text = _workingCopy.stream_urls?.FirstOrDefault() ?? "";
            GroupBox.Text = _workingCopy.group;
            LogoBox.Text = _workingCopy.logo_url;
            TvgIdBox.Text = _workingCopy.tvg_id;
            FavoriteBox.IsChecked = _workingCopy.isFavorite;
            CountryBox.Text = _workingCopy.country;
            GeoBlockedBox.IsChecked = _workingCopy.isGeoBlocked;
            LanguagesBox.Text = _workingCopy.languages != null ? string.Join(",", _workingCopy.languages) : "";
        }

        private void SaveCurrentToOriginal()
        {
            var original = _channels[_currentIndex];
            original.name = _workingCopy.name;
            original.stream_urls = _workingCopy.stream_urls;
            original.group = _workingCopy.group;
            original.logo_url = _workingCopy.logo_url;
            original.tvg_id = _workingCopy.tvg_id;
            original.isFavorite = _workingCopy.isFavorite;
            original.country = _workingCopy.country;
            original.isGeoBlocked = _workingCopy.isGeoBlocked;
            original.languages = _workingCopy.languages;
        }

        private void UpdateNavigationButtons()
        {
            PrevBtn.IsEnabled = _currentIndex > 0;
            NextBtn.IsEnabled = _currentIndex < (_channels?.Count - 1);
        }

        private void PrevBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex <= 0) return;
            SaveCurrentToOriginal();
            _currentIndex--;
            LoadChannel(_currentIndex);
        }

        private void NextBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex >= _channels.Count - 1) return;
            SaveCurrentToOriginal();
            _currentIndex++;
            LoadChannel(_currentIndex);
        }

        private void LogoBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_workingCopy != null)
                _workingCopy.logo_url = LogoBox.Text;
            LogoUrlPreview = LogoBox.Text;
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _workingCopy.name = NameBox.Text ?? "";
                _workingCopy.stream_urls = new List<string> { UrlBox.Text ?? "" };
                _workingCopy.group = GroupBox.Text ?? "";
                _workingCopy.logo_url = LogoBox.Text ?? "";
                _workingCopy.tvg_id = TvgIdBox.Text ?? "";
                _workingCopy.isFavorite = FavoriteBox.IsChecked == true;
                _workingCopy.country = CountryBox.Text ?? "";
                _workingCopy.isGeoBlocked = GeoBlockedBox.IsChecked == true;
                string langText = LanguagesBox.Text ?? "";
                _workingCopy.languages = langText.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                 .Select(l => l.Trim())
                                                 .Where(l => !string.IsNullOrEmpty(l))
                                                 .ToList();

                SaveCurrentToOriginal();
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving channel details: {ex.Message}\n\n{ex.StackTrace}",
                                "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                DialogResult = false;
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}