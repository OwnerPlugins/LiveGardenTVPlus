using LiveGardenTVPlus.Converters;
using LiveGardenTVPlus.Models;
using LiveGardenTVPlus.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace LiveGardenTVPlus.Views
{
    public partial class LogoPickerWindow : Window
    {
        public string SelectedLogoUrl { get; private set; }
        private List<LogoItemViewModel> _allItems;
        private List<LogoItemViewModel> _currentItems;

        public LogoPickerWindow(List<LogoInfo> logos)
        {
            InitializeComponent();
            var dummy = new BoolToVisibilityConverter();

            // Safety check
            if (logos == null) logos = new List<LogoInfo>();

            _allItems = logos.Select(l => new LogoItemViewModel
            {
                Name = l.Name ?? "",
                Url = l.Url ?? ""
            }).ToList();

            _currentItems = new List<LogoItemViewModel>(_allItems);
            LogosListBox.ItemsSource = _currentItems;

            // Load thumbnails after UI is ready
            this.Loaded += async (s, e) =>
            {
                await Task.Delay(200);
                await LoadAllThumbnailsAsync();
            };
        }

        private async Task LoadAllThumbnailsAsync()
        {
            if (_currentItems == null) return;

            var toLoad = _currentItems.Where(item => item != null && item.Thumbnail == null).ToList();
            foreach (var item in toLoad)
            {
                try
                {
                    await item.LoadThumbnailAsync();
                    await Task.Delay(10); // Small delay to keep UI responsive
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Thumbnail error: {ex.Message}");
                }
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            string filter = SearchBox.Text?.ToLowerInvariant() ?? "";
            if (string.IsNullOrWhiteSpace(filter))
                _currentItems = new List<LogoItemViewModel>(_allItems);
            else
                _currentItems = _allItems.Where(item => item.Name.ToLowerInvariant().Contains(filter)).ToList();

            LogosListBox.ItemsSource = _currentItems;
            _ = LoadAllThumbnailsAsync();
        }

        private void LogoItem_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var item = LogosListBox.SelectedItem as LogoItemViewModel;
            if (item != null && !string.IsNullOrEmpty(item.Url))
            {
                SelectedLogoUrl = item.Url;
                DialogResult = true;
                Close();
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void ThumbnailsOptionChanged(object sender, RoutedEventArgs e)
        {
            if (ShowThumbnailsCheckBox.IsChecked == false)
            {
                foreach (var item in _allItems)
                    item.Thumbnail = null;
                LogosListBox.Items.Refresh();
            }
            else
            {
                await LoadAllThumbnailsAsync();
            }
        }
    }

    public class LogoItemViewModel : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string Url { get; set; }

        private BitmapImage _thumbnail;
        public BitmapImage Thumbnail
        {
            get => _thumbnail;
            set { _thumbnail = value; OnPropertyChanged(); }
        }

        public async Task LoadThumbnailAsync()
        {
            if (Thumbnail != null) return;
            if (string.IsNullOrEmpty(Url)) return;

            try
            {
                var img = await ImageCache.GetImageAsync(Url);
                if (img != null)
                    Thumbnail = img;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load thumbnail for {Name}: {ex.Message}");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}