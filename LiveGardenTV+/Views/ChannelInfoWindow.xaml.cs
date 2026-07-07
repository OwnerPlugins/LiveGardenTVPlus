using LiveGardenTVPlus.Models;
using LiveGardenTVPlus.Services;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace LiveGardenTVPlus.Views
{
    public partial class ChannelInfoWindow : Window, INotifyPropertyChanged
    {
        private Channel _channel;
        private Action? _onSaveCallback;

        public string? LogoUrlPreview
        {
            get
            {
                return _channel?.Logo;
            }

            set
            {
#pragma warning disable CS8601 // Possibile assegnazione di riferimento Null.
                if (_channel != null) _channel.Logo = value ?? string.Empty;
                OnPropertyChanged(nameof(LogoUrlPreview));
            }
        }

        public ChannelInfoWindow(Channel channel, Action? onSaveCallback = null)
        {
            InitializeComponent();
            _channel = channel;
            _onSaveCallback = onSaveCallback;

            ApplyLanguage();
            LanguageManager.LanguageChanged += ApplyLanguage;

            DataContext = this;
            LoadChannelData();
        }

        private void ApplyLanguage()
        {
            Title = LanguageManager.GetTranslation("Channel Info");
            SaveBtn.Content = LanguageManager.GetTranslation("Save");
            CloseBtn.Content = LanguageManager.GetTranslation("Close");
        }

        private void LoadChannelData()
        {
            if (_channel == null) return;

            NameBox.Text = _channel.Name;
            UrlBox.Text = _channel.Url;
            GroupBox.Text = _channel.Group;
            LogoBox.Text = _channel.Logo;
            TvgIdBox.Text = _channel.TvgId;
            FavoriteBox.IsChecked = _channel.IsFavorite;
            RadioBox.IsChecked = _channel.IsRadio;
            StatusText.Text = _channel.UrlStatus ?? "Unknown";
            OnPropertyChanged(nameof(LogoUrlPreview));
        }

        private void LogoBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_channel != null)
                _channel.Logo = LogoBox.Text;
            OnPropertyChanged(nameof(LogoUrlPreview));
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_channel == null) return;

            _channel.Name = NameBox.Text;
            _channel.Url = UrlBox.Text;
            _channel.Group = GroupBox.Text;
            _channel.Logo = LogoBox.Text;
            _channel.TvgId = TvgIdBox.Text;
            _channel.IsFavorite = FavoriteBox.IsChecked == true;
            _channel.IsRadio = RadioBox.IsChecked == true;

            _onSaveCallback?.Invoke();

            MessageBox.Show(LanguageManager.GetTranslation("Channel updated successfully."),
                            LanguageManager.GetTranslation("Success"),
                            MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}