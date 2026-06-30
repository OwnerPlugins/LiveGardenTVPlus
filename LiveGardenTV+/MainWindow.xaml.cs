using LiveGardenTVPlus.Models;
using LiveGardenTVPlus.Services;
using LiveGardenTVPlus.Views;
using MaterialDesignThemes.Wpf;
using Microsoft.Web.WebView2.Core;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LiveGardenTVPlus
{
    public partial class MainWindow : Window
    {
        private List<Channel> _allChannelsOriginal = new List<Channel>();
        private bool _showFavoritesOnly = false;
        private string _searchFilter = "";
        private bool _isDrillingIntoGroup = false;
        private string _drilledGroupName = "";
        private string _currentChannelName = "";

        private System.Windows.Threading.DispatcherTimer _timeshiftTimer;
        private bool _isLiveMode = true;

        private bool _playerBackgroundHidden = false;

        private EpgService _epgService = new EpgService();
        private bool _playerReady = false;

        private Window _currentEpgDetailsWindow = null;

        public ObservableCollection<ChannelGroup> ChannelGroups { get; set; } = new ObservableCollection<ChannelGroup>();
        public List<string> YoutubeUrls { get; set; } = new List<string>();
        public List<string> StreamUrls { get; set; } = new List<string>();

        private bool _isFullscreenUIActive = false;
        private GridLength _savedChannelColumnWidth;
        private double _savedWindowHeight, _savedWindowWidth;
        private WindowState _savedWindowState;

        private bool _isFirstLoad = true;
        private bool _isApplyingLanguage = false;
        public MainWindow()
        {
            InitializeComponent();
            UpdateRecentPopup();

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            string shortVersion = $"{version.Major}.{version.Minor}";
            this.Title = $"TVGarden+ v{shortVersion}";

            ShowFavoritesOnlyToggle.IsChecked = false;
            _showFavoritesOnly = false;

            ChannelTreeView.ItemsSource = ChannelGroups;

            PlayerBackground.Visibility = Visibility.Visible;
            PlayerBackground.Source = new BitmapImage(new Uri("pack://application:,,,/Images/tv.png", UriKind.Absolute));
            WebPlayer.Visibility = Visibility.Collapsed;


            LanguageManager.LanguageChanged -= OnLanguageChanged;

            ApplyLanguage();

            var prefs = UserPreferences.Load();
            string savedLang = prefs.Language ?? "English";
            LanguageManager.LoadLanguage(savedLang);

            LanguageManager.LanguageChanged += OnLanguageChanged;

            if (!string.Equals(savedLang, "English", StringComparison.OrdinalIgnoreCase))
            {
                ApplyLanguage();
            }

            Loaded += async (s, e) =>
            {
                await InitWebView();
                var prefsLocal = UserPreferences.Load();
                var slider = BufferSlider;
                var text = BufferValueText;
                if (!string.IsNullOrEmpty(prefsLocal.EpgUrl))
                {
                    await _epgService.LoadEpgAsync(prefsLocal.EpgUrl);
                }
                if (slider != null && text != null)
                {
                    slider.Value = prefsLocal.BufferSeconds;
                    text.Text = $"{prefsLocal.BufferSeconds}s";
                }
            };
            _timeshiftTimer = new System.Windows.Threading.DispatcherTimer();
            _timeshiftTimer.Interval = TimeSpan.FromSeconds(1);
            _timeshiftTimer.Tick += OnTimeshiftTimerTick;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _timeshiftTimer?.Stop();
            if (_allChannelsOriginal != null)
                FavoritesManager.SaveFavorites(_allChannelsOriginal);
            base.OnClosing(e);
        }

        public void ApplyLanguage()
        {
            if (_isApplyingLanguage) return;
            _isApplyingLanguage = true;
            try
            {
                TranslationHelper.TranslateUI(this);
                StatusTextBlock.Text = LanguageManager.GetTranslation("Application status");
                int total = _allChannelsOriginal?.Count ?? 0;
                TotalChannelsText.Text = $"{LanguageManager.GetTranslation("Total")}: {total}";
                TotalChannelsText.ToolTip = LanguageManager.GetTranslation("Total channels in loaded playlist");
                var res = TryFindResource("ForegroundBrush");
                if (res is System.Windows.Media.Brush brush)
                {
                    TotalChannelsText.Foreground = brush;
                }
                else
                {
                    TotalChannelsText.ClearValue(TextBlock.ForegroundProperty);
                }

                // Toolbar buttons and tooltips
                LoadPlaylistBtnText.Text = LanguageManager.GetTranslation("Load File");
                LoadPlaylistBtn.ToolTip = LanguageManager.GetTranslation("Load playlist file (M3U/JSON) from your computer");

                RecentTextBlock.Text = LanguageManager.GetTranslation("Recent");
                RecentBtn.ToolTip = LanguageManager.GetTranslation("Recent playlists");

                SavePlaylistBtnText.Text = LanguageManager.GetTranslation("Save Playlist");
                SavePlaylistBtn.ToolTip = LanguageManager.GetTranslation("Save current playlist to a new M3U file");

                ExportFavoritesBtnText.Text = LanguageManager.GetTranslation("Export Favorites");
                ExportFavoritesBtn.ToolTip = LanguageManager.GetTranslation("Export only favorite channels to a new M3U file");

                SendToEnigmaText.Text = LanguageManager.GetTranslation("Send to Enigma2");
                SendToEnigmaBtn.ToolTip = LanguageManager.GetTranslation("Send playlist to Enigma2 decoder");
                PopupTelnetConsoleText.Text = LanguageManager.GetTranslation("Telnet Console");

                EditPlaylistBtnText.Text = LanguageManager.GetTranslation("Edit Playlist");
                EditPlaylistBtn.ToolTip = LanguageManager.GetTranslation("Playlist Management");

                EpgTextBlock.Text = LanguageManager.GetTranslation("EPG");
                EpgBtn.ToolTip = LanguageManager.GetTranslation("TV Guide (EPG)");

                ToolsBtnText.Text = LanguageManager.GetTranslation("Tools");
                ToolsBtn.ToolTip = LanguageManager.GetTranslation("Tools");

                AboutBtnText.Text = LanguageManager.GetTranslation("Info");
                AboutBtn.ToolTip = LanguageManager.GetTranslation("Info on");

                HelpBtnText.Text = LanguageManager.GetTranslation("Help");
                HelpBtn.ToolTip = LanguageManager.GetTranslation("Help");

                FavoritesOnlyText.Text = LanguageManager.GetTranslation("Favorites only");
                ShowFavoritesOnlyToggle.ToolTip = LanguageManager.GetTranslation("Show only channels marked as favorites");

                ToggleText.Text = (ChannelColumn.Width.Value > 0) ? LanguageManager.GetTranslation("Hide List") : LanguageManager.GetTranslation("Show List");
                ToggleSidebarBtn.ToolTip = LanguageManager.GetTranslation("Show or hide the channel list");

                SearchBox.ToolTip = LanguageManager.GetTranslation("Search channels by name");
                ClearSearchBtn.ToolTip = LanguageManager.GetTranslation("Clear search");

                // Popup menu items
                PopupSettingsText.Text = LanguageManager.GetTranslation("Settings");
                PopupThemeText.Text = LanguageManager.GetTranslation("Theme picker");
                PopupHelpText.Text = LanguageManager.GetTranslation("Help");
                PopupAboutText.Text = LanguageManager.GetTranslation("Info");
                PopupUpdateText.Text = LanguageManager.GetTranslation("Check for updates");

                // Status bar controls
                BufferLabel.Text = LanguageManager.GetTranslation("Buffer (seconds)");
                BufferSlider.ToolTip = LanguageManager.GetTranslation("Adjust buffer size (1-10 seconds)");

                PipBtn.ToolTip = LanguageManager.GetTranslation("Picture-in-Picture mode");
                FullscreenUIBtn.ToolTip = LanguageManager.GetTranslation("Fullscreen (hide UI) - press ESC to exit");
                PauseResumeBtn.ToolTip = LanguageManager.GetTranslation("Pause/Resume live stream");
                LiveBtn.ToolTip = LanguageManager.GetTranslation("Go back to live");
                TimeshiftSlider.ToolTip = LanguageManager.GetTranslation("Drag to seek back in buffer");

                EpgInfoTextBlock.Text = LanguageManager.GetTranslation("Click for details");

                SpeedSlowerBtn.ToolTip = LanguageManager.GetTranslation("Half speed (0.5x)");
                SpeedNormalBtn.ToolTip = LanguageManager.GetTranslation("Normal speed (1x)");
                SpeedFasterBtn.ToolTip = LanguageManager.GetTranslation("Double speed (2x)");

                // Window title
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                string shortVersion = $"{version.Major}.{version.Minor}";
                this.Title = $"TVGarden+ v{shortVersion} - by Lululla | CORVOBOYS.ORG | LINUXSAT-SUPPORT.COM";
            }
            finally
            {
                _isApplyingLanguage = false;
            }
        }

        private void OnLanguageChanged()
        {
            if (_isFirstLoad)
            {
                _isFirstLoad = false;
                return;
            }
            ApplyLanguage();
        }

        private void ToolsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ToolsPopup.IsOpen)
            {
                ToolsPopup.IsOpen = false;
                return;
            }
            var btn = ToolsBtn;
            var point = btn.PointToScreen(new Point(0, btn.ActualHeight));
            ToolsPopup.HorizontalOffset = point.X;
            ToolsPopup.VerticalOffset = point.Y;
            ToolsPopup.IsOpen = true;
        }

        private void PopupSettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            ToolsPopup.IsOpen = false;
            SettingsBtn_Click(sender, e);
        }

        private void PopupThemeBtn_Click(object sender, RoutedEventArgs e)
        {
            ToolsPopup.IsOpen = false;
            ThemePickerBtn_Click(sender, e);
        }

        private void PopupHelpBtn_Click(object sender, RoutedEventArgs e)
        {
            ToolsPopup.IsOpen = false;
            HelpBtn_Click(sender, e);
        }

        private void PopupAboutBtn_Click(object sender, RoutedEventArgs e)
        {
            ToolsPopup.IsOpen = false;
            AboutBtn_Click(sender, e);
        }

        private void PopupUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            ToolsPopup.IsOpen = false;
            CheckUpdatesBtn_Click(sender, e);
        }

        private async void CheckUpdatesBtn_Click(object sender, RoutedEventArgs e)
        {
            CheckForUpdates();
        }

        private async Task CheckForUpdates()
        {
            string csprojUrl = "https://raw.githubusercontent.com/OwnerPlugins/LiveGardenTVPlus/main/LiveGardenTV+/LiveGardenTV+.csproj";
            string zipUrl = "https://github.com/OwnerPlugins/LiveGardenTVPlus/raw/main/LiveGardenTVPlus.zip";
            string tempZip = Path.Combine(Path.GetTempPath(), "LiveGardenTVPlus_update.zip");
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string currentExe = Path.Combine(appDir, "LiveGardenTV+.exe");

            try
            {
                Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                if (currentVersion == null) currentVersion = new Version(1, 0);

                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("LiveGardenTVPlus");
                string csprojContent = await client.GetStringAsync(csprojUrl);
                var match = Regex.Match(csprojContent, @"<AssemblyVersion>([^<]+)</AssemblyVersion>");
                if (!match.Success)
                {
                    MessageBox.Show(LanguageManager.GetTranslation("Unable to find version in remote project file."),
                                    LanguageManager.GetTranslation("Update"),
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                Version remoteVersion = new Version(match.Groups[1].Value);

                if (remoteVersion <= currentVersion)
                {
                    string shortVersion = $"{currentVersion.Major}.{currentVersion.Minor}";
                    string msg = LanguageManager.GetTranslation("No updates available.") +
                                 $" You are using version {shortVersion}.";
                    MessageBox.Show(msg, LanguageManager.GetTranslation("Update"),
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                MessageBoxResult result = MessageBox.Show(
                    LanguageManager.GetTranslation("A new version is available. Do you want to update now?"),
                    LanguageManager.GetTranslation("Update"),
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;

                using (var response = await client.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead))
                using (var fs = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None))
                    await response.Content.CopyToAsync(fs);

                string batPath = Path.Combine(Path.GetTempPath(), "update_LiveGardenTVPlus.bat");
                using (StreamWriter sw = new StreamWriter(batPath))
                {
                    sw.WriteLine("@echo off");
                    sw.WriteLine("echo " + LanguageManager.GetTranslation("Waiting for LiveGardenTVPlus to close..."));
                    sw.WriteLine("timeout /t 2 /nobreak > nul");
                    sw.WriteLine(":loop");
                    sw.WriteLine("tasklist /fi \"imagename eq LiveGardenTVPlus.exe\" 2>NUL | find /i /n \"LiveGardenTV+.exe\">NUL");
                    sw.WriteLine("if \"%errorlevel%\"==\"0\" (");
                    sw.WriteLine("    timeout /t 1 /nobreak > nul");
                    sw.WriteLine("    goto loop");
                    sw.WriteLine(")");
                    sw.WriteLine("taskkill /f /im LiveGardenTVPlus.exe 2>nul");
                    sw.WriteLine("echo " + LanguageManager.GetTranslation("Extracting update..."));
                    sw.WriteLine($"powershell -Command \"Expand-Archive -Path '{tempZip}' -DestinationPath '{appDir}' -Force\"");
                    sw.WriteLine("if exist \"" + tempZip + "\" del \"" + tempZip + "\"");
                    sw.WriteLine("echo " + LanguageManager.GetTranslation("Update complete. Restarting..."));
                    sw.WriteLine($"start \"\" \"{currentExe}\"");
                    sw.WriteLine("del \"%~f0\"");
                }

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = batPath,
                    UseShellExecute = true,
                    CreateNoWindow = false
                };
                Process.Start(psi);

                if (WebPlayer?.CoreWebView2 != null)
                {
                    WebPlayer.CoreWebView2.Stop();
                }
                WebPlayer?.Dispose();

                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show(LanguageManager.GetTranslation("Update failed: ") + ex.Message,
                                LanguageManager.GetTranslation("Error"),
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape && _isFullscreenUIActive)
            {
                FullscreenUIBtn_Click(null, null);
                e.Handled = true;
            }
            base.OnPreviewKeyDown(e);
        }

        private async void PauseResumeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (WebPlayer?.CoreWebView2 != null)
            {
                try
                {
                    var result = await WebPlayer.CoreWebView2.ExecuteScriptAsync("togglePause()");
                    bool isPaused = result?.Replace("\"", "") == "true";
                    PauseIcon.Kind = isPaused ? PackIconKind.Play : PackIconKind.Pause;

                    if (isPaused)
                        StatusTextBlock.Text = LanguageManager.GetTranslation("Paused");
                    else
                        StatusTextBlock.Text = $"{LanguageManager.GetTranslation("Now")}: {_currentChannelName}";
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Pause error: {ex.Message}");
                }
            }
        }

        private List<Channel> ConvertJsonToChannels(List<ChannelJson> jsonChannels)
        {
            return jsonChannels.Select(ch => new Channel
            {
                Name = ch.name,
                Url = ch.stream_urls?.FirstOrDefault() ?? "",
                Logo = ch.logo_url,
                Group = ch.group ?? "General",
                TvgId = ch.tvg_id,
                IsFavorite = ch.isFavorite,
                StreamUrls = ch.stream_urls ?? new List<string>(),
                YoutubeUrls = ch.youtube_urls ?? new List<string>(),
                UrlStatus = ch.UrlStatus
            }).ToList();
        }

        private async void ChannelTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is Channel selected)
            {
                // Set player background based on channel type
                try
                {
                    string bgImage = selected.IsRadio ? "pack://application:,,,/Images/radio.png" : "pack://application:,,,/Images/tv.png";
                    PlayerBackground.Source = new BitmapImage(new Uri(bgImage, UriKind.Absolute));
                    PlayerBackground.Visibility = Visibility.Visible;
                    WebPlayer.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Background image error: {ex.Message}");
                    PlayerBackground.Source = new BitmapImage(new Uri("pack://application:,,,/Images/tv.png", UriKind.Absolute));
                }

                if (selected.Name == "← Back to all groups")
                {
                    _isDrillingIntoGroup = false;
                    _drilledGroupName = "";
                    RefreshChannelsView();
                    return;
                }

                if (selected.YoutubeUrls != null && selected.YoutubeUrls.Count > 0)
                {
                    var youtubeUrl = selected.YoutubeUrls.First();
                    var result = MessageBox.Show(
                        "This is a YouTube video. Open in external browser?",
                        "YouTube",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                        Process.Start(new ProcessStartInfo { FileName = youtubeUrl, UseShellExecute = true });
                    return;
                }

                if (WebPlayer?.CoreWebView2 != null && !string.IsNullOrEmpty(selected.Url))
                {
                    if (!_playerReady)
                    {
                        StatusTextBlock.Text = "Player is loading. Please wait...";
                        Logger.Info("Playback attempted before player ready.");
                        return;
                    }

                    try
                    {
                        string js = $"playStream('{selected.Url.Replace("'", "\\'")}');";
                        await WebPlayer.CoreWebView2.ExecuteScriptAsync(js);
                        Debug.WriteLine($"Playing URL: {selected.Url}");
                        StatusTextBlock.Text = $"{LanguageManager.GetTranslation("Now")}: {selected.Name}";
                        _currentChannelName = selected.Name;

                        // Hide background for TV (video) streams, keep visible for radio
                        if (selected.IsRadio)
                        {
                            WebPlayer.Visibility = Visibility.Collapsed;
                            PlayerBackground.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            PlayerBackground.Visibility = Visibility.Collapsed;
                            WebPlayer.Visibility = Visibility.Visible;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Playback error: {ex.Message}");
                        StatusTextBlock.Text = $"Error: {ex.Message}";
                        MessageBox.Show($"Cannot play stream: {ex.Message}", "Playback Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else if (e.NewValue is ChannelGroup group)
            {
                _isDrillingIntoGroup = true;
                _drilledGroupName = group.GroupName ?? "General";
                RefreshChannelsView();
            }

            // EPG update
            if (e.NewValue is Channel ch)
            {
                var program = _epgService.GetCurrentProgram(ch.Name, ch.TvgId, DateTime.UtcNow);
                if (program != null)
                {
                    var startLocal = program.Start.ToLocalTime();
                    var stopLocal = program.Stop.ToLocalTime();
                    EpgInfoTextBlock.Text = $"{program.Title} | {startLocal:HH:mm} - {stopLocal:HH:mm}";
                    EpgInfoTextBlock.Foreground = (Brush)FindResource("ForegroundBrush");
                    EpgIcon.Foreground = Brushes.LightGreen;
                }
                else
                {
                    EpgInfoTextBlock.Text = LanguageManager.GetTranslation("No EPG data");
                    EpgInfoTextBlock.Foreground = Brushes.Gray;
                    EpgIcon.Foreground = Brushes.Gray;
                }
            }
        }

        private async void EpgInfoPanel_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;

            var currentChannel = ChannelTreeView.SelectedItem as Channel;
            if (currentChannel == null) return;

            var now = DateTime.UtcNow;
            var currentProgram = _epgService.GetCurrentProgram(currentChannel.Name, currentChannel.TvgId, now);
            if (currentProgram == null)
            {
                MessageBox.Show(
                    LanguageManager.GetTranslation("No EPG information available for this channel."),
                    LanguageManager.GetTranslation("Info"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            // Trova il programma successivo
            string epgChannelId = currentChannel.TvgId ?? _epgService.GetMappedEpgId(currentChannel.Name);
            var epgChannel = _epgService.GetChannelById(epgChannelId);
            EpgProgramme nextProgram = null;
            if (epgChannel != null)
            {
                nextProgram = epgChannel.Programmes
                    .Where(p => p.Start > currentProgram.Stop)
                    .OrderBy(p => p.Start)
                    .FirstOrDefault();
            }

            ShowEpgDetailsWindow(currentChannel, currentProgram, nextProgram);
        }

        private void ShowEpgDetailsWindow(Channel channel, EpgProgramme current, EpgProgramme next)
        {
            // Prevent multiple windows
            if (_currentEpgDetailsWindow != null && _currentEpgDetailsWindow.IsVisible)
            {
                _currentEpgDetailsWindow.Focus();
                return;
            }

            var win = new Window
            {
                Title = LanguageManager.GetTranslation("EPG Details"),
                Width = 450,  // double.NaN, // Auto
                Height = double.NaN,         // Auto
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = (Brush)FindResource("WindowBackgroundBrush"),
                Foreground = (Brush)FindResource("ForegroundBrush")
            };

            var grid = new Grid { Margin = new Thickness(15) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // per next description

            // Channel name
            var channelNameBlock = new TextBlock
            {
                Text = channel.Name,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = (Brush)FindResource("AccentBrush")
            };
            Grid.SetRow(channelNameBlock, 0);
            grid.Children.Add(channelNameBlock);

            // Current program
            var nowLocal = current.Start.ToLocalTime();
            var stopLocal = current.Stop.ToLocalTime();
            var currentBlock = new TextBlock
            {
                Text = $"{LanguageManager.GetTranslation("Now")}: {current.Title}\n{nowLocal:HH:mm} - {stopLocal:HH:mm}",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = (Brush)FindResource("ForegroundBrush")
            };
            Grid.SetRow(currentBlock, 1);
            grid.Children.Add(currentBlock);

            // Current program description (italic)
            if (!string.IsNullOrEmpty(current.Description))
            {
                var descBlock = new TextBlock
                {
                    Text = current.Description,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 15),
                    FontStyle = FontStyles.Italic,
                    Foreground = (Brush)FindResource("ForegroundBrush")
                };
                Grid.SetRow(descBlock, 2);
                grid.Children.Add(descBlock);
            }

            // Next program (if exists)
            if (next != null)
            {
                var nextStartLocal = next.Start.ToLocalTime();
                var nextStopLocal = next.Stop.ToLocalTime();
                var nextBlock = new TextBlock
                {
                    Text = $"{LanguageManager.GetTranslation("Next")}: {next.Title}\n{nextStartLocal:HH:mm} - {nextStopLocal:HH:mm}",
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 10),
                    Foreground = (Brush)FindResource("ForegroundBrush")
                };
                Grid.SetRow(nextBlock, 3);
                grid.Children.Add(nextBlock);

                // Next program description (italic)
                if (!string.IsNullOrEmpty(next.Description))
                {
                    var nextDescBlock = new TextBlock
                    {
                        Text = next.Description,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 15),
                        FontStyle = FontStyles.Italic,
                        Foreground = (Brush)FindResource("ForegroundBrush")
                    };
                    Grid.SetRow(nextDescBlock, 4);
                    grid.Children.Add(nextDescBlock);
                }
            }

            // Close button
            var closeBtn = new Button
            {
                Content = LanguageManager.GetTranslation("Close"),
                Width = 80,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            closeBtn.Click += (s, args) => win.Close();
            Grid.SetRow(closeBtn, 5);
            grid.Children.Add(closeBtn);

            win.Content = grid;
            _currentEpgDetailsWindow = win;
            win.Closed += (s, e) => _currentEpgDetailsWindow = null;
            win.ShowDialog();
        }
        private void AddProgramDetails(StackPanel parent, EpgProgramme prog)
        {
            var startLocal = prog.Start.ToLocalTime();
            var stopLocal = prog.Stop.ToLocalTime();
            parent.Children.Add(new TextBlock
            {
                Text = $"{LanguageManager.GetTranslation("Title")}: {prog.Title}",
                Margin = new Thickness(0, 0, 0, 5)
            });
            parent.Children.Add(new TextBlock
            {
                Text = $"{LanguageManager.GetTranslation("Time")}: {startLocal:dd/MM HH:mm} - {stopLocal:HH:mm}",
                Margin = new Thickness(0, 0, 0, 5)
            });
            if (!string.IsNullOrEmpty(prog.Category))
                parent.Children.Add(new TextBlock
                {
                    Text = $"{LanguageManager.GetTranslation("Category")}: {prog.Category}",
                    Margin = new Thickness(0, 0, 0, 5)
                });
            parent.Children.Add(new TextBlock
            {
                Text = $"{LanguageManager.GetTranslation("Description")}:",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 10, 0, 5)
            });
            parent.Children.Add(new TextBlock
            {
                Text = prog.Description ?? LanguageManager.GetTranslation("(No description)"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });
        }

        private async Task InitWebView()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PlayerHost", "player.html");
            if (!File.Exists(htmlPath))
            {
                StatusTextBlock.Text = LanguageManager.GetTranslation("ERROR: player.html not found.");
                _playerReady = false;
                return;
            }

            try
            {
                string html = File.ReadAllText(htmlPath);
                var options = new CoreWebView2EnvironmentOptions("--disable-web-security");
                var env = await CoreWebView2Environment.CreateAsync(null, null, options);
                await WebPlayer.EnsureCoreWebView2Async(env);

                WebPlayer.CoreWebView2.NavigationCompleted += (s, e) =>
                {
                    var prefs = UserPreferences.Load();
                    WebPlayer.CoreWebView2.ExecuteScriptAsync($"if(window.hls) window.hls.config.maxBufferLength = {prefs.BufferSeconds};");
                    WebPlayer.CoreWebView2.ExecuteScriptAsync("video.playbackRate = 1;");
                    _playerReady = true;
                    _timeshiftTimer.Start();
                };

                // Navigate to the HTML string
                WebPlayer.CoreWebView2.NavigateToString(html);
                StatusTextBlock.Text = LanguageManager.GetTranslation("Loading player...");
            }
            catch (Exception ex)
            {
                Logger.Error($"InitWebView error: {ex.Message}");
                StatusTextBlock.Text = $"WebView2 error: {ex.Message}";
                _playerReady = false;
            }
        }

        private void ExpandRootNode()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (ChannelTreeView.HasItems)
                {
                    var item = ChannelTreeView.ItemContainerGenerator.ContainerFromIndex(0) as TreeViewItem;
                    if (item != null)
                        item.IsExpanded = true;
                    else
                    {
                        EventHandler? handler = null;
                        handler = (s, e) =>
                        {
                            var itm = ChannelTreeView.ItemContainerGenerator.ContainerFromIndex(0) as TreeViewItem;
                            if (itm != null)
                            {
                                itm.IsExpanded = true;
                                ChannelTreeView.ItemContainerGenerator.StatusChanged -= handler;
                            }
                        };
                        ChannelTreeView.ItemContainerGenerator.StatusChanged += handler;
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void RefreshChannelsView()
        {
            ChannelGroups.Clear();

            if (_allChannelsOriginal == null || _allChannelsOriginal.Count == 0)
            {
                TotalChannelsText.Text = $"{LanguageManager.GetTranslation("Total")}: 0";
                return;
            }

            var query = _allChannelsOriginal.AsEnumerable();

            if (_showFavoritesOnly)
                query = query.Where(c => c.IsFavorite);

            bool isSearchActive = !string.IsNullOrEmpty(_searchFilter) && _searchFilter != LanguageManager.GetTranslation("Search channels...");

            if (isSearchActive)
            {
                var results = query
                    .Where(c => c.Name.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
                var flatGroup = new ChannelGroup
                {
                    GroupName = $"🔍 Results for \"{_searchFilter}\" ({results.Count})",
                    Channels = new ObservableCollection<Channel>(results)
                };
                ChannelGroups.Add(flatGroup);
                TotalChannelsText.Text = $"{LanguageManager.GetTranslation("Total")}: {results.Count}";
                return;
            }

            if (_isDrillingIntoGroup && !string.IsNullOrEmpty(_drilledGroupName))
            {
                var groupChannels = query
                    .Where(c => (string.IsNullOrEmpty(c.Group) ? "General" : c.Group) == _drilledGroupName)
                    .ToList();
                var fakeGroup = new ChannelGroup
                {
                    GroupName = "",
                    Channels = new ObservableCollection<Channel>()
                };
                fakeGroup.Channels.Add(new Channel
                {
                    Name = "← Back to all groups",
                    Url = null,
                    Group = _drilledGroupName
                });
                foreach (var ch in groupChannels)
                    fakeGroup.Channels.Add(ch);
                ChannelGroups.Add(fakeGroup);
                ExpandRootNode();
                // Total = only the actual channels in the group, excluding the navigation row
                TotalChannelsText.Text = $"{LanguageManager.GetTranslation("Total")}: {groupChannels.Count}";
                return;
            }

            var groups = query
                .GroupBy(c => string.IsNullOrEmpty(c.Group) ? "General" : c.Group)
                .Select(g => new ChannelGroup
                {
                    GroupName = g.Key,
                    Channels = new ObservableCollection<Channel>(g)
                })
                .ToList();

            foreach (var group in groups)
                ChannelGroups.Add(group);

            // Total = sum of channels of all groups (no dummy elements)
            int total = groups.Sum(g => g.Channels.Count);
            TotalChannelsText.Text = $"{LanguageManager.GetTranslation("Total")}: {total}";
        }

        private void LoadPlaylist(string filePath)
        {
            try
            {
                ShowLoading(true);
                string extension = System.IO.Path.GetExtension(filePath).ToLower();

                if (extension == ".json")
                {
                    // Use the unified import service (centralized logic)
                    var mappedChannels = JsonImportService.ImportFromFileWithMapping(filePath, this);
                    if (mappedChannels != null && mappedChannels.Count > 0)
                    {
                        // Convert to Channel list (simple version, without extra fields)
                        var channels = JsonImportService.ConvertToChannelListSimple(mappedChannels);
                        _allChannelsOriginal = channels;
                        _showFavoritesOnly = false;
                        ShowFavoritesOnlyToggle.IsChecked = false;
                        _searchFilter = "";
                        _isDrillingIntoGroup = false;
                        _drilledGroupName = "";
                        SearchBox.Text = LanguageManager.GetTranslation("Search channels...");
                        SearchBox.Foreground = Brushes.Gray;
                        ClearSearchBtn.Visibility = Visibility.Collapsed;

                        FavoritesManager.ApplyFavorites(_allChannelsOriginal);
                        RefreshChannelsView();

                        StatusTextBlock.Text = $"{LanguageManager.GetTranslation("Loaded")} {channels.Count} {LanguageManager.GetTranslation("channels from")} {Path.GetFileName(filePath)}";
                        AddToRecent(filePath);
                    }
                    else
                    {
                        // User cancelled or no channels mapped
                        StatusTextBlock.Text = LanguageManager.GetTranslation("Import cancelled or no channels mapped.");
                    }
                    return;
                }
                else
                {
                    // M3U handling (existing logic)
                    var channels = M3uParser.Parse(filePath);

                    string epgUrl = M3uParser.EpgUrl;
                    if (!string.IsNullOrEmpty(epgUrl))
                    {
                        var prefs = UserPreferences.Load();
                        prefs.EpgUrl = epgUrl;
                        prefs.Save();
                        _ = _epgService.LoadEpgAsync(epgUrl);
                    }

                    if (channels.Count == 0) throw new Exception("No channels found.");
                    _allChannelsOriginal = channels;
                    _showFavoritesOnly = false;
                    ShowFavoritesOnlyToggle.IsChecked = false;
                    _searchFilter = "";
                    _isDrillingIntoGroup = false;
                    _drilledGroupName = "";
                    SearchBox.Text = LanguageManager.GetTranslation("Search channels...");
                    SearchBox.Foreground = Brushes.Gray;
                    ClearSearchBtn.Visibility = Visibility.Collapsed;

                    FavoritesManager.ApplyFavorites(_allChannelsOriginal);
                    RefreshChannelsView();

                    StatusTextBlock.Text = $"{LanguageManager.GetTranslation("Loaded")} {channels.Count} {LanguageManager.GetTranslation("channels from")} {Path.GetFileName(filePath)}";
                }

                AddToRecent(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{LanguageManager.GetTranslation("Error")}: {ex.Message}");
            }
            finally
            {
                ShowLoading(false);
            }
            TotalChannelsText.Text = $"{LanguageManager.GetTranslation("Total")}: {_allChannelsOriginal.Count}";
        }

        public async Task LoadPlaylistFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                StatusTextBlock.Text = LanguageManager.GetTranslation("Invalid URL.");
                return;
            }
            try
            {
                ShowLoading(true);
                StatusTextBlock.Text = LanguageManager.GetTranslation("Downloading playlist...");
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(60);
                    client.DefaultRequestHeaders.Add("User-Agent", "LiveGardenTVPlus/1.0");
                    string content = await client.GetStringAsync(url);

                    // Remove BOM if present
                    if (content.StartsWith("\uFEFF"))
                        content = content.Substring(1);
                    content = content.Trim();

                    // Detect JSON vs M3U
                    bool isJson = (content.StartsWith("{") || content.StartsWith("[")) && !content.StartsWith("#EXTM3U");

                    if (isJson)
                    {
                        var mappedChannels = JsonImportService.ImportFromUrlWithMapping(content, "url_import.json", this);
                        if (mappedChannels != null && mappedChannels.Count > 0)
                        {
                            var channels = JsonImportService.ConvertToChannelListSimple(mappedChannels);
                            _allChannelsOriginal = channels;
                            _showFavoritesOnly = false;
                            ShowFavoritesOnlyToggle.IsChecked = false;
                            _searchFilter = "";
                            _isDrillingIntoGroup = false;
                            _drilledGroupName = "";
                            SearchBox.Text = LanguageManager.GetTranslation("Search channels...");
                            SearchBox.Foreground = Brushes.Gray;
                            ClearSearchBtn.Visibility = Visibility.Collapsed;

                            FavoritesManager.ApplyFavorites(_allChannelsOriginal);
                            await Dispatcher.InvokeAsync(() =>
                            {
                                RefreshChannelsView();
                                StatusTextBlock.Text = $"{LanguageManager.GetTranslation("Loaded")} {channels.Count} {LanguageManager.GetTranslation("channels from JSON URL")}";
                            });
                        }
                        return;
                    }
                    else
                    {
                        // M3U handling (existing code)
                        string tempFile = Path.GetTempFileName();
                        await File.WriteAllTextAsync(tempFile, content);
                        var channels = M3uParser.Parse(tempFile);
                        File.Delete(tempFile);

                        if (channels == null || channels.Count == 0)
                            throw new Exception("No channels found in playlist.");

                        string epgUrl = M3uParser.EpgUrl;
                        if (!string.IsNullOrEmpty(epgUrl))
                        {
                            var prefs = UserPreferences.Load();
                            prefs.EpgUrl = epgUrl;
                            prefs.Save();
                            _ = _epgService.LoadEpgAsync(epgUrl);
                        }

                        _allChannelsOriginal = channels;
                        _showFavoritesOnly = false;
                        ShowFavoritesOnlyToggle.IsChecked = false;
                        _searchFilter = "";
                        _isDrillingIntoGroup = false;
                        _drilledGroupName = "";
                        SearchBox.Text = LanguageManager.GetTranslation("Search channels...");
                        SearchBox.Foreground = Brushes.Gray;
                        ClearSearchBtn.Visibility = Visibility.Collapsed;

                        FavoritesManager.ApplyFavorites(_allChannelsOriginal);
                        await Dispatcher.InvokeAsync(() =>
                        {
                            RefreshChannelsView();
                            StatusTextBlock.Text = $"{LanguageManager.GetTranslation("Loaded")} {channels.Count} {LanguageManager.GetTranslation("channels from URL")}";
                        });
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                StatusTextBlock.Text = $"{LanguageManager.GetTranslation("Network error")}: {ex.Message}";
                MessageBox.Show($"{LanguageManager.GetTranslation("Failed to download playlist")}\n{ex.Message}",
                                LanguageManager.GetTranslation("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"{LanguageManager.GetTranslation("Error")}: {ex.Message}";
                MessageBox.Show($"{LanguageManager.GetTranslation("Failed to load playlist")}\n{ex.Message}",
                                LanguageManager.GetTranslation("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ShowLoading(false);
            }
            TotalChannelsText.Text = $"{LanguageManager.GetTranslation("Total")}: {_allChannelsOriginal.Count}";
        }

        private void SavePlaylistBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_allChannelsOriginal == null || _allChannelsOriginal.Count == 0)
            {
                MessageBox.Show(LanguageManager.GetTranslation("Load a playlist first."),
                                LanguageManager.GetTranslation("Info"),
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "M3U files|*.m3u",
                DefaultExt = ".m3u",
                FileName = "exported_playlist.m3u"
            };
            if (dialog.ShowDialog() == true)
            {
                ExportToM3u(dialog.FileName, _allChannelsOriginal);
                MessageBox.Show($"Playlist saved to {dialog.FileName}",
                                LanguageManager.GetTranslation("Success"),
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CopyNameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selected = ChannelTreeView.SelectedItem as Channel;
            if (selected != null)
            {
                Clipboard.SetText(selected.Name);
                StatusTextBlock.Text = $"{LanguageManager.GetTranslation("Copied")}: {selected.Name}";
            }
        }

        private void CopyUrlMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selected = ChannelTreeView.SelectedItem as Channel;
            if (selected != null && !string.IsNullOrEmpty(selected.Url))
            {
                Clipboard.SetText(selected.Url);
                StatusTextBlock.Text = $"{LanguageManager.GetTranslation("Copied")}: {selected.Url}";
            }
        }

        private async void MoveToGroupMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selected = ChannelTreeView.SelectedItem as Channel;
            if (selected == null) return;

            var groups = _allChannelsOriginal.Select(c => c.Group).Distinct().OrderBy(g => g).ToList();
            groups.Insert(0, "General");

            var dialog = new Window
            {
                Title = LanguageManager.GetTranslation("Move to Group"),
                Width = 300,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = (Brush)FindResource("WindowBackgroundBrush"),
                Foreground = (Brush)FindResource("ForegroundBrush")
            };
            var grid = new Grid { Margin = new Thickness(10) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock { Text = LanguageManager.GetTranslation("Select target group:"), Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(label, 0);
            grid.Children.Add(label);

            var combo = new ComboBox { ItemsSource = groups, SelectedIndex = 0, Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(combo, 1);
            grid.Children.Add(combo);

            var panel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okBtn = new Button { Content = "OK", Width = 80, Margin = new Thickness(0, 0, 10, 0), IsDefault = true };
            var cancelBtn = new Button { Content = "Cancel", Width = 80, IsCancel = true };
            panel.Children.Add(okBtn);
            panel.Children.Add(cancelBtn);
            Grid.SetRow(panel, 2);
            grid.Children.Add(panel);

            dialog.Content = grid;
            okBtn.Click += (s, args) => { dialog.DialogResult = true; dialog.Close(); };
            cancelBtn.Click += (s, args) => { dialog.DialogResult = false; dialog.Close(); };

            if (dialog.ShowDialog() == true)
            {
                string newGroup = combo.SelectedItem as string;
                if (!string.IsNullOrEmpty(newGroup))
                {
                    selected.Group = newGroup;
                    FavoritesManager.SaveFavorites(_allChannelsOriginal);
                    RefreshChannelsView();
                    StatusTextBlock.Text = $"{LanguageManager.GetTranslation("Moved")} {selected.Name} → {newGroup}";
                }
            }
        }

        private async void DeleteChannelMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selected = ChannelTreeView.SelectedItem as Channel;
            if (selected == null) return;

            var result = MessageBox.Show(
                string.Format(LanguageManager.GetTranslation("Delete channel '{0}'?"), selected.Name),
                LanguageManager.GetTranslation("Confirm Delete"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _allChannelsOriginal.Remove(selected);
                RefreshChannelsView();
                FavoritesManager.SaveFavorites(_allChannelsOriginal);
                StatusTextBlock.Text = $"{LanguageManager.GetTranslation("Deleted")}: {selected.Name}";
            }
        }

        private async void ExportChannelMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selected = ChannelTreeView.SelectedItem as Channel;
            if (selected == null) return;

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "M3U files|*.m3u|JSON files|*.json",
                DefaultExt = ".m3u",
                FileName = $"{selected.Name.Replace(" ", "_")}"
            };
            if (dialog.ShowDialog() == true)
            {
                string ext = Path.GetExtension(dialog.FileName).ToLower();
                if (ext == ".m3u")
                {
                    var channels = new List<Channel> { selected };
                    ExportToM3u(dialog.FileName, channels);
                }
                else if (ext == ".json")
                {
                    var json = new List<ChannelJson>
                    {
                        new ChannelJson
                        {
                            name = selected.Name,
                            stream_urls = new List<string> { selected.Url },
                            logo_url = selected.Logo,
                            group = selected.Group,
                            tvg_id = selected.TvgId,
                            isFavorite = selected.IsFavorite
                        }
                    };
                    string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(json, Newtonsoft.Json.Formatting.Indented);
                    await File.WriteAllTextAsync(dialog.FileName, jsonString);
                }
                StatusTextBlock.Text = $"{LanguageManager.GetTranslation("Exported")}: {selected.Name}";
            }
        }

        public void ShowLoading(bool show)
        {
            Dispatcher.Invoke(() =>
            {
                LoadingProgressBar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                LoadPlaylistBtn.IsEnabled = !show;
                EditPlaylistBtn.IsEnabled = !show;
                SavePlaylistBtn.IsEnabled = !show;
                ExportFavoritesBtn.IsEnabled = !show;
                ToggleSidebarBtn.IsEnabled = !show;
                ToolsBtn.IsEnabled = !show;
                SearchBox.IsEnabled = !show;
                PauseResumeBtn.IsEnabled = !show;
                SpeedSlowerBtn.IsEnabled = !show;
                SpeedNormalBtn.IsEnabled = !show;
                SpeedFasterBtn.IsEnabled = !show;
                PipBtn.IsEnabled = !show;
                FullscreenUIBtn.IsEnabled = !show;
                AboutBtn.IsEnabled = !show;
            });
        }

        private void ExportToM3u(string filePath, List<Channel> channels)
        {
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("#EXTM3U");
                foreach (var ch in channels)
                {
                    writer.WriteLine($"#EXTINF:-1 group-title=\"{ch.Group}\" tvg-logo=\"{ch.Logo}\" tvg-id=\"{ch.TvgId}\",{ch.Name}");
                    writer.WriteLine(ch.Url);
                }
            }
        }

        private async void SpeedSlowerBtn_Click(object sender, RoutedEventArgs e)
        {
            if (WebPlayer?.CoreWebView2 != null)
                await WebPlayer.CoreWebView2.ExecuteScriptAsync("video.playbackRate = 0.5;");
        }

        private async void SpeedNormalBtn_Click(object sender, RoutedEventArgs e)
        {
            if (WebPlayer?.CoreWebView2 != null)
                await WebPlayer.CoreWebView2.ExecuteScriptAsync("video.playbackRate = 1;");
        }

        private async void SpeedFasterBtn_Click(object sender, RoutedEventArgs e)
        {
            if (WebPlayer?.CoreWebView2 != null)
                await WebPlayer.CoreWebView2.ExecuteScriptAsync("video.playbackRate = 2;");
        }

        private void BufferSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (BufferValueText == null) return;
            int seconds = (int)BufferSlider.Value;
            BufferValueText.Text = $"{seconds}s";
            var prefs = UserPreferences.Load();
            prefs.BufferSeconds = seconds;
            prefs.Save();
            if (WebPlayer?.CoreWebView2 != null)
                WebPlayer.CoreWebView2.ExecuteScriptAsync($"if(window.hls) window.hls.config.maxBufferLength = {seconds};");
        }

        private async void PipBtn_Click(object sender, RoutedEventArgs e)
        {
            if (WebPlayer?.CoreWebView2 != null)
                await WebPlayer.CoreWebView2.ExecuteScriptAsync("video.requestPictureInPicture();");
        }

        private void FullscreenUIBtn_Click(object sender, RoutedEventArgs e)
        {
            var mainToolBar = FindName("MainToolBar") as FrameworkElement;
            var mainStatusBar = FindName("MainStatusBar") as FrameworkElement;

            _isFullscreenUIActive = !_isFullscreenUIActive;
            if (_isFullscreenUIActive)
            {
                _savedChannelColumnWidth = ChannelColumn.Width;
                _savedWindowHeight = this.Height;
                _savedWindowWidth = this.Width;
                _savedWindowState = this.WindowState;

                if (mainToolBar != null) mainToolBar.Visibility = Visibility.Collapsed;
                if (mainStatusBar != null) mainStatusBar.Visibility = Visibility.Collapsed;
                ChannelColumn.Width = new GridLength(0);

                this.WindowState = WindowState.Normal;
                this.WindowStyle = WindowStyle.None;
                this.WindowState = WindowState.Maximized;
            }
            else
            {
                if (mainToolBar != null) mainToolBar.Visibility = Visibility.Visible;
                if (mainStatusBar != null) mainStatusBar.Visibility = Visibility.Visible;
                ChannelColumn.Width = _savedChannelColumnWidth;

                this.WindowStyle = WindowStyle.SingleBorderWindow;
                this.WindowState = _savedWindowState;
                this.Height = _savedWindowHeight;
                this.Width = _savedWindowWidth;
            }
        }

        private void LoadPlaylistBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Playlist files|*.m3u;*.m3u8;*.json|M3U files|*.m3u;*.m3u8|JSON files|*.json",
                DefaultExt = ".m3u"
            };
            if (dlg.ShowDialog() == true)
            {
                string filePath = dlg.FileName;
                string ext = Path.GetExtension(filePath).ToLower();

                if (ext == ".json")
                {
                    // Use the same mapping window
                    string json = File.ReadAllText(filePath, Encoding.UTF8);
                    var mappingWindow = new Views.JsonImportMappingWindow(json, filePath);
                    mappingWindow.Owner = this;
                    if (mappingWindow.ShowDialog() == true)
                    {
                        var mappedChannels = mappingWindow.GetMappedChannels();
                        if (mappedChannels != null && mappedChannels.Count > 0)
                        {
                            // Convert to List<Channel> for MainWindow
                            var channels = mappedChannels.Select(ch => new Channel
                            {
                                Name = ch.name,
                                Url = ch.stream_urls?.FirstOrDefault() ?? "",
                                Logo = ch.logo_url,
                                Group = ch.group,
                                TvgId = ch.tvg_id,
                                IsFavorite = ch.isFavorite
                            }).ToList();

                            _allChannelsOriginal = channels;
                            FavoritesManager.ApplyFavorites(_allChannelsOriginal);
                            RefreshChannelsView();
                            StatusTextBlock.Text = $"{LanguageManager.GetTranslation("Loaded")} {channels.Count} {LanguageManager.GetTranslation("channels from JSON")}";
                        }
                    }
                }
                else
                {
                    // M3U handling (existing code)
                    LoadPlaylist(filePath);
                }
            }
        }

        private void UpdateRecentPopup()
        {
            var prefs = UserPreferences.Load();
            var items = prefs.RecentPlaylists
                .Where(File.Exists)
                .Select(path => new { DisplayName = System.IO.Path.GetFileName(path), FilePath = path })
                .ToList();
            RecentListBox.ItemsSource = items;
        }

        private void AddToRecent(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return;
            var prefs = UserPreferences.Load();
            var list = prefs.RecentPlaylists;
            list.Remove(filePath);
            list.Insert(0, filePath);
            while (list.Count > 5)
                list.RemoveAt(list.Count - 1);
            prefs.Save();
            UpdateRecentPopup();
        }

        private void RecentBtn_Click(object sender, RoutedEventArgs e)
        {
            UpdateRecentPopup();
            var btn = RecentBtn;
            var point = btn.PointToScreen(new Point(0, btn.ActualHeight));
            RecentPopup.HorizontalOffset = point.X;
            RecentPopup.VerticalOffset = point.Y;
            RecentPopup.IsOpen = true;
        }

        private void RecentListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RecentListBox.SelectedItem == null) return;
            dynamic selected = RecentListBox.SelectedItem;
            string path = selected.FilePath;
            RecentPopup.IsOpen = false;
            if (File.Exists(path))
                LoadPlaylist(path);
            else
                MessageBox.Show("File not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            RecentListBox.SelectedItem = null;
        }

        private async void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow();
            win.Owner = this;
            if (win.ShowDialog() == true && !string.IsNullOrEmpty(win.SelectedPlaylistUrl))
            {
                await LoadPlaylistFromUrl(win.SelectedPlaylistUrl);
            }
        }

        private void ThemePickerBtn_Click(object sender, RoutedEventArgs e)
        {
            var picker = new ColorPickerWindow();
            picker.Owner = this;
            if (picker.ShowDialog() == true)
            {
                ThemeManager.SetTheme(picker.SelectedTheme);
                var prefs = UserPreferences.Load();
                prefs.Theme = picker.SelectedTheme;
                prefs.Save();
            }
        }

        private void ToggleSidebarBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ChannelColumn.Width.Value > 0)
            {
                ChannelColumn.Width = new GridLength(0);
                ToggleText.Text = LanguageManager.GetTranslation("Show List");
                ToggleIcon.Kind = PackIconKind.ArrowExpandRight;
            }
            else
            {
                ChannelColumn.Width = new GridLength(280);
                ToggleText.Text = LanguageManager.GetTranslation("Hide List");
                ToggleIcon.Kind = PackIconKind.ArrowCollapseLeft;
            }
        }

        private void EditPlaylistBtn_Click(object sender, RoutedEventArgs e)
        {
            var channels = _allChannelsOriginal ?? new List<Channel>();
            var editor = new Views.PlaylistEditorWindow(channels, _epgService);  // pass the EPG service
            editor.Owner = this;
            editor.ShowDialog();

            if (editor.IsSaved && !string.IsNullOrEmpty(editor.SavedFilePath))
            {
                var result = MessageBox.Show(
                    LanguageManager.GetTranslation("Do you want to load this edited playlist now?"),
                    LanguageManager.GetTranslation("Apply changes"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    LoadPlaylist(editor.SavedFilePath);
                }
            }
        }

        private void FavoriteStar_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var channel = button?.DataContext as Channel;
            if (channel == null) return;

            channel.IsFavorite = !channel.IsFavorite;
            FavoritesManager.SaveFavorites(_allChannelsOriginal);
        }

        private void ExportFavoritesBtn_Click(object sender, RoutedEventArgs e)
        {
            var favorites = _allChannelsOriginal?.Where(c => c.IsFavorite).ToList();
            if (favorites == null || favorites.Count == 0)
            {
                MessageBox.Show(LanguageManager.GetTranslation("No favorite channels to export."),
                                LanguageManager.GetTranslation("Info"),
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "M3U files|*.m3u",
                DefaultExt = ".m3u",
                FileName = "favorites_playlist.m3u"
            };
            if (dialog.ShowDialog() == true)
            {
                ExportToM3u(dialog.FileName, favorites);
                MessageBox.Show($"{favorites.Count} favorite channels exported to {dialog.FileName}",
                                LanguageManager.GetTranslation("Success"),
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ShowFavoritesOnly_Click(object sender, RoutedEventArgs e)
        {
            var toggle = sender as ToggleButton;
            if (toggle != null)
                _showFavoritesOnly = toggle.IsChecked == true;
            RefreshChannelsView();
        }

        private async void SendToEnigmaBtn_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("=== SendToEnigmaBtn_Click START ===");
            Logger.Info("SendToEnigma started.");

            if (_allChannelsOriginal == null || _allChannelsOriginal.Count == 0)
            {
                Debug.WriteLine("No playlist loaded.");
                Logger.Error("No playlist loaded.");
                MessageBox.Show(LanguageManager.GetTranslation("Load a playlist first."));
                return;
            }
            Debug.WriteLine($"Playlist loaded: {_allChannelsOriginal.Count} channels");
            Logger.Info($"Playlist loaded: {_allChannelsOriginal.Count} channels");

            var prefs = UserPreferences.Load();

            // Check Host
            if (string.IsNullOrEmpty(prefs.TelnetHost))
            {
                Debug.WriteLine("Telnet host not configured.");
                Logger.Error("Telnet host not configured.");
                MessageBox.Show(LanguageManager.GetTranslation("Telnet host not configured. Please set it in Settings."),
                                LanguageManager.GetTranslation("Configuration Missing"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check Username
            if (string.IsNullOrEmpty(prefs.TelnetUser))
            {
                Debug.WriteLine("Telnet username not configured.");
                Logger.Error("Telnet username not configured.");
                MessageBox.Show(LanguageManager.GetTranslation("Telnet username not configured. Please set it in Settings."),
                                LanguageManager.GetTranslation("Configuration Missing"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check Password (allow empty with warning)
            if (string.IsNullOrEmpty(prefs.TelnetPass))
            {
                Debug.WriteLine("Telnet password is empty.");
                Logger.Info("Telnet password is empty.");
                var result = MessageBox.Show(
                    LanguageManager.GetTranslation("Telnet password is empty. Do you want to continue?"),
                    LanguageManager.GetTranslation("Empty Password"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes)
                {
                    Logger.Info("User cancelled due to empty password.");
                    return;
                }
            }

            Debug.WriteLine($"Telnet host: {prefs.TelnetHost}, Port: {prefs.TelnetPort}, User: {prefs.TelnetUser}");
            Logger.Info($"Telnet host: {prefs.TelnetHost}, Port: {prefs.TelnetPort}, User: {prefs.TelnetUser}");

            string bouquetName = InputBoxHelper.ShowInputBox(
                LanguageManager.GetTranslation("Enter bouquet name (without spaces):"),
                LanguageManager.GetTranslation("Bouquet Name"),
                "LiveGardenTV+");
            if (string.IsNullOrWhiteSpace(bouquetName)) return;
            Debug.WriteLine($"Bouquet name: {bouquetName}");
            Logger.Info($"Bouquet name: {bouquetName}");

            string remoteFile = $"/etc/enigma2/userbouquet.{bouquetName}.tv";
            Debug.WriteLine($"Remote file: {remoteFile}");
            Logger.Info($"Remote file: {remoteFile}");

            bool exists = await FileExistsOnFtp(remoteFile);
            Debug.WriteLine($"File exists on FTP: {exists}");
            Logger.Info($"File exists on FTP: {exists}");
            if (exists)
            {
                var result = MessageBox.Show(
                    string.Format(LanguageManager.GetTranslation("File {0} already exists. Overwrite?"), remoteFile),
                    LanguageManager.GetTranslation("File exists"),
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                Logger.Info($"Overwrite response: {(result == MessageBoxResult.Yes ? "YES" : "NO")}");
                if (result != MessageBoxResult.Yes) return;
            }

            string content = GenerateEnigma2Bouquet(bouquetName);
            Debug.WriteLine($"Bouquet generated, length: {content.Length} chars");
            Logger.Info($"Bouquet generated, length: {content.Length} chars");

            Debug.WriteLine("Uploading bouquet via FTP...");
            bool uploaded = await UploadBouquetViaFtp(content, remoteFile);
            Debug.WriteLine($"Upload result: {(uploaded ? "SUCCESS" : "FAILURE")}");
            Logger.Info($"Upload result: {(uploaded ? "SUCCESS" : "FAILURE")}");
            if (!uploaded)
            {
                Logger.Error("Failed to upload bouquet.");
                MessageBox.Show(
                    LanguageManager.GetTranslation("Failed to upload bouquet. Please check your Telnet settings (host, port, username, password) and try again."),
                    LanguageManager.GetTranslation("Upload Error"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }
            Debug.WriteLine("Updating bouquets.tv reference...");
            bool refUpdated = await UpdateBouquetsTv(bouquetName);
            Debug.WriteLine($"Update bouquets.tv result: {(refUpdated ? "SUCCESS" : "FAILURE")}");
            Logger.Info($"Update bouquets.tv result: {(refUpdated ? "SUCCESS" : "FAILURE")}");
            if (!refUpdated)
            {
                Logger.Error("Failed to update bouquets.tv.");
                MessageBox.Show(
                    LanguageManager.GetTranslation("Failed to update bouquets.tv. Please check file permissions on the Enigma2 device."),
                    LanguageManager.GetTranslation("Update Error"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            Debug.WriteLine("Reloading Enigma2 channel list via HTTP...");
            await ReloadEnigma2Channels();
            Logger.Success("Bouquet sent and Enigma2 channel list reloaded.");
            Debug.WriteLine("Reload command sent.");

            MessageBox.Show(LanguageManager.GetTranslation("Bouquet sent and Enigma2 channel list reloaded."));
            Debug.WriteLine("=== SendToEnigmaBtn_Click END (SUCCESS) ===");
        }

        private async Task<bool> FileExistsOnFtp(string remotePath)
        {
            var prefs = UserPreferences.Load();
            string ftpUrl = $"ftp://{prefs.TelnetHost}/{remotePath}";
            try
            {
                var request = (FtpWebRequest)WebRequest.Create(ftpUrl);
                request.Method = WebRequestMethods.Ftp.GetFileSize;
                request.Credentials = new NetworkCredential(prefs.TelnetUser, prefs.TelnetPass);
                using (var response = (FtpWebResponse)await request.GetResponseAsync())
                    return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FileExists check: {ex.Message}");
                return false;
            }
        }

        private string GenerateEnigma2Bouquet(string bouquetName)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"#NAME {bouquetName}");
            foreach (var ch in _allChannelsOriginal)
            {
                if (string.IsNullOrEmpty(ch.Url)) continue;
                string encodedUrl = ch.Url.Replace(":", "%3a");
                sb.AppendLine($"#SERVICE 4097:0:1:0:0:0:0:0:0:0:{encodedUrl}");
                sb.AppendLine($"#DESCRIPTION {ch.Name}");
            }
            return sb.ToString();
        }

        private async Task<bool> UpdateBouquetsTv(string bouquetName)
        {
            var prefs = UserPreferences.Load();
            string remoteBouquets = "/etc/enigma2/bouquets.tv";
            string localTemp = Path.GetTempFileName();
            string content = "";

            bool exists = await FileExistsOnFtp(remoteBouquets);
            if (exists)
            {
                try
                {
                    var request = (FtpWebRequest)WebRequest.Create($"ftp://{prefs.TelnetHost}/{remoteBouquets}");
                    request.Method = WebRequestMethods.Ftp.DownloadFile;
                    request.Credentials = new NetworkCredential(prefs.TelnetUser, prefs.TelnetPass);
                    using (var response = await request.GetResponseAsync())
                    using (var stream = response.GetResponseStream())
                    using (var reader = new StreamReader(stream))
                        content = await reader.ReadToEndAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Download bouquets.tv error: {ex.Message}");
                    return false;
                }
            }

            // Split lines, remove empty ones
            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            var nonEmptyLines = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            string referenceLine = $"#SERVICE 1:7:1:0:0:0:0:0:0:0:FROM BOUQUET \"userbouquet.{bouquetName}.tv\" ORDER BY bouquet";

            // If already present, return true
            if (nonEmptyLines.Any(l => l.Contains($"userbouquet.{bouquetName}.tv")))
                return true;

            // Add to the end
            nonEmptyLines.Add(referenceLine);
            string newContent = string.Join("\n", nonEmptyLines) + "\n";

            return await UploadBouquetViaFtp(newContent, remoteBouquets);
        }

        private async Task<bool> UploadBouquetViaFtp(string content, string remotePath)
        {
            var prefs = UserPreferences.Load();
            string ftpUrl = $"ftp://{prefs.TelnetHost}/{remotePath}";
            try
            {
                var request = (FtpWebRequest)WebRequest.Create(ftpUrl);
                request.Method = WebRequestMethods.Ftp.UploadFile;
                request.Credentials = new NetworkCredential(prefs.TelnetUser, prefs.TelnetPass);
                request.UseBinary = true;
                byte[] data = Encoding.UTF8.GetBytes(content);
                request.ContentLength = data.Length;
                using (var stream = await request.GetRequestStreamAsync())
                    await stream.WriteAsync(data, 0, data.Length);
                using (var response = (FtpWebResponse)await request.GetResponseAsync())
                {
                    // Accept both 226 (ClosingData) and 226 (FileActionOK)
                    bool success = response.StatusCode == FtpStatusCode.FileActionOK ||
                                   response.StatusCode == FtpStatusCode.ClosingData;
                    Debug.WriteLine($"FTP response: {response.StatusCode} - {response.StatusDescription}");
                    return success;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FTP upload error: {ex.Message}");
                return false;
            }
        }

        /*
        private async Task ReloadEnigma2Channels()
        {
            var prefs = UserPreferences.Load();
            string url = $"http://{prefs.TelnetHost}/web/servicelistreload?mode=0";
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    var response = await client.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"Reload HTTP OK: {response.StatusCode}");
                    }
                    else
                    {
                        Debug.WriteLine($"Reload HTTP failed: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Reload HTTP exception: {ex.Message}");
            }
        }*/

        private async Task ReloadEnigma2Channels()
        {
            try
            {
                var prefs = UserPreferences.Load();
                string url = $"http://{prefs.TelnetHost}/web/servicelistreload?mode=0";
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    var response = await client.GetAsync(url);
                    Debug.WriteLine($"Reload HTTP OK: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Reload HTTP error: {ex.Message}");
                // Non rilanciare l'eccezione – l'operazione è già riuscita
            }
        }

        private async Task ReloadEnigma2Channels2()
        {
            var prefs = UserPreferences.Load();
            using (var client = new TcpClient())
            {
                await client.ConnectAsync(prefs.TelnetHost, prefs.TelnetPort);
                using (var stream = client.GetStream())
                {
                    // Login sequence
                    byte[] buffer = Encoding.ASCII.GetBytes(prefs.TelnetUser + "\r\n");
                    await stream.WriteAsync(buffer, 0, buffer.Length);
                    await Task.Delay(200);
                    buffer = Encoding.ASCII.GetBytes(prefs.TelnetPass + "\r\n");
                    await stream.WriteAsync(buffer, 0, buffer.Length);
                    await Task.Delay(500);
                    // Send reload command
                    buffer = Encoding.ASCII.GetBytes("wget -qO- http://127.0.0.1/web/servicelistreload?mode=0\r\n");
                    await stream.WriteAsync(buffer, 0, buffer.Length);
                    await Task.Delay(2000);
                    // No need to read response
                }
            }
        }

        private async Task<bool> DownloadFileViaFtp(string remotePath, string localPath)
        {
            var prefs = UserPreferences.Load();
            string ftpUrl = $"ftp://{prefs.TelnetHost}/{remotePath}";
            try
            {
                var request = (System.Net.FtpWebRequest)System.Net.WebRequest.Create(ftpUrl);
                request.Method = System.Net.WebRequestMethods.Ftp.DownloadFile;
                request.Credentials = new System.Net.NetworkCredential(prefs.TelnetUser, prefs.TelnetPass);
                using (var response = await request.GetResponseAsync())
                using (var stream = response.GetResponseStream())
                using (var file = File.Create(localPath))
                    await stream.CopyToAsync(file);
                return true;
            }
            catch { return false; }
        }

        private void PopupTelnetConsoleBtn_Click(object sender, RoutedEventArgs e)
        {
            ToolsPopup.IsOpen = false;
            var telnetConsole = new TelnetConsoleWindow();
            telnetConsole.Owner = this;
            telnetConsole.ShowDialog();
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SearchBox.Text == LanguageManager.GetTranslation("Search channels..."))
            {
                SearchBox.Text = "";
                SearchBox.Foreground = Brushes.Black;
            }
            ClearSearchBtn.Visibility = Visibility.Visible;
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                SearchBox.Text = LanguageManager.GetTranslation("Search channels...");
                SearchBox.Foreground = Brushes.Gray;
                ClearSearchBtn.Visibility = Visibility.Collapsed;
                if (_searchFilter != "")
                {
                    _searchFilter = "";
                    RefreshChannelsView();
                }
            }
        }

        private void ClearSearchBtn_Click(object sender, RoutedEventArgs e)
        {
            _searchFilter = "";
            SearchBox.Text = "";
            SearchBox.Foreground = Brushes.Black;
            RefreshChannelsView();
            SearchBox.Focus();
            ClearSearchBtn.IsEnabled = false; // disable when empty
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchFilter = SearchBox.Text;
            ClearSearchBtn.IsEnabled = !string.IsNullOrEmpty(SearchBox.Text) && SearchBox.Text != LanguageManager.GetTranslation("Search channels...");
            RefreshChannelsView();
        }

        private void EpgBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_allChannelsOriginal == null || _allChannelsOriginal.Count == 0)
            {
                MessageBox.Show(LanguageManager.GetTranslation("Load a playlist first."),
                                LanguageManager.GetTranslation("Info"),
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var epgWindow = new Views.EpgWindow(_epgService, _allChannelsOriginal);
            epgWindow.Owner = this;
            epgWindow.ShowDialog();
        }

        private void AboutBtn_Click(object sender, RoutedEventArgs e)
        {
            var about = new LiveGardenTVPlus.Views.AboutWindow();
            about.Owner = this;
            about.ShowDialog();
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0 && (Path.GetExtension(files[0]).ToLower() == ".m3u" || Path.GetExtension(files[0]).ToLower() == ".m3u8"))
                    LoadPlaylist(files[0]);
            }
        }

        private async void OnTimeshiftTimerTick(object sender, EventArgs e)
        {
            if (WebPlayer?.CoreWebView2 == null || !_playerReady) return;
            try
            {
                var check = await WebPlayer.CoreWebView2.ExecuteScriptAsync("typeof getCurrentTime === 'function'");
                if (check != "true") return;

                var currentTimeStr = await WebPlayer.CoreWebView2.ExecuteScriptAsync("getCurrentTime()");
                var bufferInfoStr = await WebPlayer.CoreWebView2.ExecuteScriptAsync("getBufferInfo()");

                if (string.IsNullOrEmpty(currentTimeStr) || string.IsNullOrEmpty(bufferInfoStr)) return;

                double current = double.Parse(currentTimeStr.Trim('"'), System.Globalization.CultureInfo.InvariantCulture);
                dynamic buffer = Newtonsoft.Json.JsonConvert.DeserializeObject(bufferInfoStr);
                double bufStart = (double)buffer.start;
                double bufEnd = (double)buffer.end;

                await Dispatcher.InvokeAsync(() =>
                {
                    if (bufEnd > bufStart && bufEnd > 0)
                    {
                        TimeshiftSlider.Visibility = Visibility.Visible;
                        LiveBtn.Visibility = Visibility.Visible;
                        TimeshiftSlider.Minimum = bufStart;
                        TimeshiftSlider.Maximum = bufEnd;
                        TimeshiftSlider.Value = current;
                        double liveThreshold = Math.Max(0, bufEnd - 3);
                        _isLiveMode = (current >= liveThreshold);
                    }
                    else
                    {
                        TimeshiftSlider.Visibility = Visibility.Collapsed;
                        LiveBtn.Visibility = Visibility.Collapsed;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Timeshift error: {ex.Message}");
            }
        }

        private async void TimeshiftSlider_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (WebPlayer?.CoreWebView2 != null)
            {
                double newTime = TimeshiftSlider.Value;
                await WebPlayer.CoreWebView2.ExecuteScriptAsync($"seekToTime({newTime})");
                _isLiveMode = false;
            }
        }

        private async void LiveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (WebPlayer?.CoreWebView2 != null)
            {
                await WebPlayer.CoreWebView2.ExecuteScriptAsync("seekToLive()");
                _isLiveMode = true;
            }
        }

        private void HelpBtn_Click(object sender, RoutedEventArgs e)
        {
            var helpWindow = new Views.HelpWindow();
            helpWindow.Owner = this;
            helpWindow.ShowDialog();
        }
    }
}