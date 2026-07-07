using LiveGardenTVPlus.Models;
using LiveGardenTVPlus.Services;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace LiveGardenTVPlus.Views
{
    public partial class EpgWindow : Window
    {
        private EpgService _epgService;
        private List<Channel> _channels;
        private List<EpgChannelDisplay> _allChannelDisplays;
        private DispatcherTimer _searchTimer;
        private List<KeyValuePair<string, string>> _epgSources = new List<KeyValuePair<string, string>>();

        public EpgWindow(EpgService epgService, List<Channel> channels)
        {
            InitializeComponent();
            _epgService = epgService;
            _channels = channels;

            // Load EPG sources list (non-blocking, but no UI interaction yet)
            _ = LoadEpgSourcesAsync();

            // Build channel display list (without EPG info initially)
            _allChannelDisplays = _channels.Select(ch => new EpgChannelDisplay
            {
                Channel = ch,
                DisplayName = ch.Name,
                EpgId = null!
            }).OrderBy(x => x.DisplayName).ToList();

            FilterChannelList();

            // Setup search debouncer
            _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _searchTimer.Tick += (s, e) => { _searchTimer.Stop(); FilterChannelList(); };

            LanguageManager.LanguageChanged += OnLanguageChanged;
            ApplyLanguage();

            // Defer EPG source check and dialog opening until after window is loaded
            this.Loaded += async (s, e) => await CheckAndLoadEpgAsync();
        }

        private async Task CheckAndLoadEpgAsync()
        {
            var prefs = UserPreferences.Load();
            if (string.IsNullOrEmpty(prefs.EpgUrl))
            {
                var result = MessageBox.Show(
                    LanguageManager.GetTranslation("No EPG source configured. Would you like to open settings to select one?"),
                    LanguageManager.GetTranslation("EPG Source Missing"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    var settings = new SettingsWindow();
                    settings.Owner = this;
                    settings.ShowDialog();
                    // Reload EPG if now configured
                    if (!string.IsNullOrEmpty(UserPreferences.Load().EpgUrl))
                        await RefreshEpgData();
                }
                // If user chooses No, continue without EPG (channel list works)
            }
            else
            {
                await RefreshEpgData();
            }
        }

        private async Task LoadEpgSourcesAsync()
        {
            try
            {
                RefreshEpgListBtn.IsEnabled = false;
                RefreshEpgListBtn.Content = LanguageManager.GetTranslation("Loading...");
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
                EpgSourceCombo.ItemsSource = _epgSources;
                EpgSourceCombo.DisplayMemberPath = "Key";
                EpgSourceCombo.SelectedValuePath = "Value";

                var prefs = UserPreferences.Load();
                if (!string.IsNullOrEmpty(prefs.EpgUrl))
                {
                    var existing = _epgSources.Find(x => x.Value == prefs.EpgUrl);
                    if (existing.Key != null)
                        EpgSourceCombo.SelectedItem = existing;
                    else
                        EpgSourceCombo.Text = prefs.EpgUrl;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading EPG list: {ex.Message}");
            }
            finally
            {
                RefreshEpgListBtn.IsEnabled = true;
                RefreshEpgListBtn.Content = LanguageManager.GetTranslation("Refresh List");
            }
        }

        private async void EpgSourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EpgSourceCombo.SelectedItem is KeyValuePair<string, string> selected)
            {
                var prefs = UserPreferences.Load();
                prefs.EpgUrl = selected.Value;
                prefs.Save();
                await RefreshEpgData();
            }
        }

        private async Task RefreshEpgData()
        {
            var prefs = UserPreferences.Load();
            if (string.IsNullOrEmpty(prefs.EpgUrl))
            {
                MessageBox.Show(LanguageManager.GetTranslation("No EPG source configured. Please select one from the combo or go to Settings."),
                                LanguageManager.GetTranslation("Info"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await _epgService.LoadEpgAsync(prefs.EpgUrl);

            // Update EPG IDs for all channels
            foreach (var item in _allChannelDisplays)
            {
                if (item.Channel != null)
                {
                    var epgId = GetEpgChannelId(item.Channel);
                    item.EpgId = epgId ?? string.Empty;
                    item.DisplayName = epgId != null ? item.Channel.Name : item.Channel.Name + " (No EPG)";
                }
                else
                {
                    item.EpgId = string.Empty;
                    item.DisplayName = "Unknown (No EPG)";
                }
            }
            FilterChannelList();
        }

        private void OnLanguageChanged() => ApplyLanguage();

        private void ApplyLanguage()
        {
            Title = LanguageManager.GetTranslation("TV Guide (EPG)");
            EpgSourceLabel.Text = LanguageManager.GetTranslation("EPG Source");
            RefreshEpgListBtn.Content = LanguageManager.GetTranslation("Refresh List");
            SearchLabel.Text = LanguageManager.GetTranslation("Search channel");
            SearchBox.ToolTip = LanguageManager.GetTranslation("Type channel name to filter");
            ClearSearchBtn.ToolTip = LanguageManager.GetTranslation("Clear search");
            RefreshBtn.Content = LanguageManager.GetTranslation("Refresh EPG");
            DetailsBtn.Content = LanguageManager.GetTranslation("Details");

            // Update DataGrid columns
            var startColumn = ProgramsGrid.Columns.FirstOrDefault(c => c.Header?.ToString() == "Start");
            if (startColumn != null) startColumn.Header = LanguageManager.GetTranslation("Start");
            var endColumn = ProgramsGrid.Columns.FirstOrDefault(c => c.Header?.ToString() == "End");
            if (endColumn != null) endColumn.Header = LanguageManager.GetTranslation("End");
            var titleColumn = ProgramsGrid.Columns.FirstOrDefault(c => c.Header?.ToString() == "Title");
            if (titleColumn != null) titleColumn.Header = LanguageManager.GetTranslation("Title");
            var descColumn = ProgramsGrid.Columns.FirstOrDefault(c => c.Header?.ToString() == "Description");
            if (descColumn != null) descColumn.Header = LanguageManager.GetTranslation("Description");
            var catColumn = ProgramsGrid.Columns.FirstOrDefault(c => c.Header?.ToString() == "Category");
            if (catColumn != null) catColumn.Header = LanguageManager.GetTranslation("Category");
        }

        private string? GetEpgChannelId(Channel ch)
        {
            if (!string.IsNullOrEmpty(ch.TvgId) && _epgService.HasChannel(ch.TvgId))
                return ch.TvgId;
            // Try fuzzy matching
            var epgId = _epgService.GetMappedEpgId(ch.Name);
            if (!string.IsNullOrEmpty(epgId))
                return epgId;
            return null;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        private void ClearSearchBtn_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
            FilterChannelList();
            SearchBox.Focus();
        }

        private void FilterChannelList()
        {
            string filter = SearchBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(filter))
            {
                ChannelListBox.ItemsSource = _allChannelDisplays;
            }
            else
            {
                var filtered = _allChannelDisplays
                    .Where(c => c.DisplayName != null && c.DisplayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
                ChannelListBox.ItemsSource = filtered;
            }
        }

        private async void ChannelListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ChannelListBox.SelectedItem is EpgChannelDisplay selected && selected.EpgId != null)
            {
                var programs = await _epgService.GetProgramsForChannelAsync(selected.EpgId, DateTime.Today, DateTime.Today.AddDays(1));
                var list = programs.Select(p => new EpgProgramDisplay
                {
                    Title = p.Title,
                    Description = p.Description,
                    Category = p.Category,
                    StartLocal = p.Start.ToLocalTime(),
                    StopLocal = p.Stop.ToLocalTime()
                }).OrderBy(p => p.StartLocal).ToList();
                ProgramsGrid.ItemsSource = list;
            }
            else
            {
                ProgramsGrid.ItemsSource = null;
            }
        }

        private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            await RefreshEpgData();
        }

        private async void RefreshEpgListBtn_Click(object sender, RoutedEventArgs e)
        {
            await LoadEpgSourcesAsync();
        }

        private void DetailsBtn_Click(object sender, RoutedEventArgs e)
        {
            var program = ProgramsGrid.SelectedItem as EpgProgramDisplay;
            if (program == null)
            {
                MessageBox.Show(LanguageManager.GetTranslation("Please select a program first."),
                                LanguageManager.GetTranslation("Info"),
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            ShowProgramDetails(program);
        }

        private void ShowProgramDetails(EpgProgramDisplay program)
        {
            var detailWindow = new Window
            {
                Title = LanguageManager.GetTranslation("Program Details"),
                Width = 500,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Content = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = new StackPanel
                    {
                        Margin = new Thickness(10),
                        Children =
                        {
                            new TextBlock { Text = program.Title, FontWeight = FontWeights.Bold, FontSize = 16, Margin = new Thickness(0,0,0,10) },
                            new TextBlock { Text = $"{LanguageManager.GetTranslation("Time")}: {program.StartLocal:HH:mm} - {program.StopLocal:HH:mm}", Margin = new Thickness(0,0,0,5) },
                            new TextBlock { Text = $"{LanguageManager.GetTranslation("Category")}: {program.Category}", Margin = new Thickness(0,0,0,10) },
                            new TextBlock { Text = LanguageManager.GetTranslation("Description") + ":", FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,5) },
                            new TextBlock { Text = program.Description ?? LanguageManager.GetTranslation("(No description)"), TextWrapping = TextWrapping.Wrap }
                        }
                    }
                }
            };
            detailWindow.ShowDialog();
        }

        private void ProgramsGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var program = ProgramsGrid.SelectedItem as EpgProgramDisplay;
            if (program != null)
                ShowProgramDetails(program);
        }

        public class EpgChannelDisplay
        {
            public Channel? Channel { get; set; }
            public string? DisplayName { get; set; }
            public string? EpgId { get; set; }
        }

        public class EpgProgramDisplay
        {
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public DateTime StartLocal { get; set; }
            public DateTime StopLocal { get; set; }
        }
    }
}