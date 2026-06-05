using System;
using LiveGardenTVPlus.Models;
using LiveGardenTVPlus.Services;
using LiveGardenTVPlus.Views;
using MaterialDesignThemes.Wpf;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Web.WebView2.Core;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;
using System.ComponentModel;

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
        
        private EpgService _epgService = new EpgService();
        private bool _playerReady = false;

        public ObservableCollection<ChannelGroup> ChannelGroups { get; set; } = new ObservableCollection<ChannelGroup>();
        public List<string> YoutubeUrls { get; set; } = new List<string>();
        public List<string> StreamUrls { get; set; } = new List<string>();

        private bool _isFullscreenUIActive = false;
        private GridLength _savedChannelColumnWidth;
        private double _savedWindowHeight, _savedWindowWidth;
        private WindowState _savedWindowState;

        public MainWindow()
        {
            InitializeComponent();

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            string shortVersion = $"{version.Major}.{version.Minor}";
            this.Title = $"TVGarden+ v{shortVersion}";
            /*
            if (CheckUpdatesBtn != null)
                CheckUpdatesBtn.ToolTip = $"Check for updates (current: {shortVersion})";
            */

            ChannelTreeView.ItemsSource = ChannelGroups;

            // Subscribe to language change event
            LanguageManager.LanguageChanged += OnLanguageChanged;

            // Load saved language (default to English)
            var prefs = UserPreferences.Load();
            string savedLang = prefs.Language;
            if (string.IsNullOrEmpty(savedLang))
                savedLang = "English";

            LanguageManager.LoadLanguage(savedLang);
            ApplyLanguage();

            Loaded += async (s, e) =>
            {
                await InitWebView();
                var prefsLocal = UserPreferences.Load();
                var slider = BufferSlider;
                var text = BufferValueText;
                if (slider != null && text != null)
                {
                    slider.Value = prefsLocal.BufferSeconds;
                    text.Text = $"{prefsLocal.BufferSeconds}s";
                }
            };
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_allChannelsOriginal != null)
                FavoritesManager.SaveFavorites(_allChannelsOriginal);
            base.OnClosing(e);
        }

        public void ApplyLanguage()
        {
            TranslationHelper.TranslateUI(this);
            if (!string.IsNullOrEmpty(_currentChannelName))
                StreamNameStatus.Text = _currentChannelName;
            else
                StreamNameStatus.Text = LanguageManager.GetTranslation("No stream");

            PauseResumeBtn.ToolTip = LanguageManager.GetTranslation("Pause/Resume live stream");
            EditPlaylistBtnText.Text = LanguageManager.GetTranslation("Edit Playlist");
            SavePlaylistBtnText.Text = LanguageManager.GetTranslation("Save Playlist");
            ExportFavoritesBtnText.Text = LanguageManager.GetTranslation("Export Favorites");
            /* AboutBtnText.Text = LanguageManager.GetTranslation("About"); */
            ToolsBtnText.Text = LanguageManager.GetTranslation("Tools");
            PopupSettingsText.Text = LanguageManager.GetTranslation("Settings");
            PopupThemeText.Text = LanguageManager.GetTranslation("Theme picker");
            PopupHelpText.Text = LanguageManager.GetTranslation("Help");
            PopupAboutText.Text = LanguageManager.GetTranslation("About");
            PopupUpdateText.Text = LanguageManager.GetTranslation("Check for updates");
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            string shortVersion = $"{version.Major}.{version.Minor}";
            this.Title = $"TVGarden+ v{shortVersion}";
        }

        private void OnLanguageChanged()
        {
            ApplyLanguage();
        }

        private void ToolsBtn_Click(object sender, RoutedEventArgs e)
        {
            ToolsPopup.IsOpen = !ToolsPopup.IsOpen;
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
                    // Execute JavaScript togglePause() and get the result
                    var result = await WebPlayer.CoreWebView2.ExecuteScriptAsync("togglePause()");
                    bool isPaused = result?.Replace("\"", "") == "true";
                    // Update icon
                    PauseIcon.Kind = isPaused ? PackIconKind.Play : PackIconKind.Pause;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Pause error: {ex.Message}");
                }
            }
        }

        private async void ChannelTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is Channel selected)
            {
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
                    string js = $"playStream('{selected.Url.Replace("'", "\\'")}');";
                    await WebPlayer.CoreWebView2.ExecuteScriptAsync(js);
                    Debug.WriteLine($"Playing URL: {selected.Url}");
                    StatusTextBlock.Text = $"{LanguageManager.GetTranslation("Now playing")}: {selected.Name}";
                    StreamNameStatus.Text = selected.Name;
                    _currentChannelName = selected.Name;
                }
            }
            else if (e.NewValue is ChannelGroup group)
            {
                _isDrillingIntoGroup = true;
                _drilledGroupName = group.GroupName ?? "General";
                RefreshChannelsView();
            }

            // --- EPG update ---
            if (e.NewValue is Channel ch)
            {
                var program = _epgService.GetCurrentProgram(ch.TvgId, DateTime.UtcNow);
                if (program != null)
                {
                    var startLocal = program.Start.ToLocalTime();
                    var stopLocal = program.Stop.ToLocalTime();
                    EpgInfoTextBlock.Text = $"{program.Title} | {startLocal:HH:mm} - {stopLocal:HH:mm}";
                }
                else
                {
                    EpgInfoTextBlock.Text = "No EPG data";
                }
            }
        }

        private async Task InitWebView()
        {
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PlayerHost", "player.html");
            if (File.Exists(htmlPath))
            {
                string html = File.ReadAllText(htmlPath);
                
                var options = new CoreWebView2EnvironmentOptions("--disable-web-security");
                var env = await CoreWebView2Environment.CreateAsync(null, null, options);
                await WebPlayer.EnsureCoreWebView2Async(env);
                
                WebPlayer.CoreWebView2.NavigateToString(html);
                StatusTextBlock.Text = LanguageManager.GetTranslation("Player ready.");
                WebPlayer.CoreWebView2.NavigationCompleted += (s, e) =>
                {
                    var prefs = UserPreferences.Load();
                    WebPlayer.CoreWebView2.ExecuteScriptAsync($"if(window.hls) window.hls.config.maxBufferLength = {prefs.BufferSeconds};");
                    WebPlayer.CoreWebView2.ExecuteScriptAsync("video.playbackRate = 1;");
                    _playerReady = true;
                };
            }
            else
            {
                StatusTextBlock.Text = LanguageManager.GetTranslation("ERROR: player.html not found.");
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
                return;

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
        }

        private void LoadPlaylist(string filePath)
        {
            try
            {
                ShowLoading(true);
                var channels = M3uParser.Parse(filePath);

                // EPG
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
            catch (Exception ex) { MessageBox.Show($"{LanguageManager.GetTranslation("Error")}: {ex.Message}");
            }
            finally
            {
                ShowLoading(false);
            }
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
                /*
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    var content = await client.GetStringAsync(url);
                    if (string.IsNullOrWhiteSpace(content))
                        throw new Exception("Empty response from server.");

                    var tempFile = Path.GetTempFileName();
                    await File.WriteAllTextAsync(tempFile, content);
                    var channels = M3uParser.Parse(tempFile);
                    File.Delete(tempFile);
                */

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(60);
                    client.DefaultRequestHeaders.Add("User-Agent", "HbbTV/1.6.1");
                    string content = await client.GetStringAsync(url);
                    if (string.IsNullOrWhiteSpace(content))
                        throw new Exception("Empty response from server.");

                    // Parsing in background
                    List<Channel> channels = null;
                    await Task.Run(() =>
                    {
                        string tempFile = Path.GetTempFileName();
                        File.WriteAllText(tempFile, content);
                        channels = M3uParser.Parse(tempFile);
                        File.Delete(tempFile);
                    });

                    if (channels == null || channels.Count == 0)
                        throw new Exception("No channels found in playlist.");

                    // EPG
                    string epgUrl = M3uParser.EpgUrl;
                    if (!string.IsNullOrEmpty(epgUrl))
                    {
                        var prefs = UserPreferences.Load();
                        prefs.EpgUrl = epgUrl;
                        prefs.Save();
                        _ = _epgService.LoadEpgAsync(epgUrl);
                    }

                    _allChannelsOriginal = channels;
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

        public void ShowLoading(bool show)
        {
            Dispatcher.Invoke(() =>
            {
                LoadingProgressBar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                LoadPlaylistBtn.IsEnabled = !show;
                LoadOnlineBtn.IsEnabled = !show;
                EditPlaylistBtn.IsEnabled = !show;
                SavePlaylistBtn.IsEnabled = !show;
                ExportFavoritesBtn.IsEnabled = !show;
                ToggleSidebarBtn.IsEnabled = !show;
                ToolsBtn.IsEnabled = !show;
                ShowFavoritesOnlyCheckBox.IsEnabled = !show;
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
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "M3U|*.m3u;*.m3u8" };
            if (dlg.ShowDialog() == true)
                LoadPlaylist(dlg.FileName);
        }

        private async void LoadOnlineBtn_Click(object sender, RoutedEventArgs e)
        {
            string url = Microsoft.VisualBasic.Interaction.InputBox(LanguageManager.GetTranslation("Enter M3U URL:"), LanguageManager.GetTranslation("Online Playlist"), "");
            if (!string.IsNullOrEmpty(url))
                await LoadPlaylistFromUrl(url);
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
            var editor = new Views.PlaylistEditorWindow(channels);
            editor.Owner = this;
            editor.ShowDialog();

            if (editor.IsSaved && !string.IsNullOrEmpty(editor.SavedFilePath))
            {
                var result = MessageBox.Show(
                    LanguageManager.GetTranslation("Do you want to load the edited playlist now?"),
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
            _showFavoritesOnly = ShowFavoritesOnlyCheckBox.IsChecked == true;
            RefreshChannelsView();
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

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchFilter = SearchBox.Text;
            ClearSearchBtn.Visibility = (string.IsNullOrEmpty(_searchFilter) || _searchFilter == LanguageManager.GetTranslation("Search channels...")) ? Visibility.Collapsed : Visibility.Visible;
            RefreshChannelsView();
        }

        private void ClearSearchBtn_Click(object sender, RoutedEventArgs e)
        {
            _searchFilter = "";
            SearchBox.Text = LanguageManager.GetTranslation("Search channels...");
            SearchBox.Foreground = Brushes.Gray;
            ClearSearchBtn.Visibility = Visibility.Collapsed;
            RefreshChannelsView();
            SearchBox.Focus();
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

        private void HelpBtn_Click(object sender, RoutedEventArgs e)
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            string shortVersion = $"{version.Major}.{version.Minor}";

            string help = $@"LiveGardenTVPlus Help - Version {shortVersion}

        📁 PLAYLIST
        ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        • Load M3U   – Open a playlist file from your PC
        • Online M3U – Enter URL of a remote playlist
        • Settings   – Change buffer size, select online playlist (Refresh from GitHub)
        • Drag & drop an M3U file directly onto the window

        ⚙️ SETTINGS
        ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        • Language – Change UI language (dynamic translation)
        • Buffer – Adjust HLS buffer (1–10 seconds)
        • Online Playlist – Select a pre‑defined M3U from GitHub
        • Refresh from GitHub – Update the list of available playlists
        • Save – Save all settings and load the selected playlist

        📺 CHANNEL VIEW
        ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        • EPG (current program info) and channel logos (tvg-logo)
        • Resizable sidebar, improved M3U parser, and update system
        • Click a channel to start playback
        • Click a group name → shows only channels of that group
        • '← Back to all groups' returns to full list
        • Search box → flat list of results (clear text to return)
        • Favorites – right‑click or use the star icon; toggle 'Favorites only'

        🎮 PLAYER CONTROLS
        ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        • Speed buttons: 0.5×, 1×, 2×
        • Buffer slider: adjust HLS buffer (1–10 seconds)
        • PIP – Picture‑in‑Picture mode
        • Fullscreen – hides all UI (click again or press ESC to restore)
        • Hide List – collapse the channel sidebar
        • Pause – Pause/Resume live stream (timeshift)

        🔄 UPDATER
        ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        • Automatic update check on startup (or click the update button)
        • If a new version is found, you will be prompted to download it
        • The updater downloads the ZIP, replaces all files, and restarts the app
        • Your settings and playlists are preserved

        🎨 THEMES & LANGUAGE
        ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        • Theme picker – 16 colour themes + Light/Dark mode
        • Language selector in Settings (dynamic translation)

        🙏 CREDITS
        ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        • Developer: Lululla
        • Playlist repository: OwnerPlugins / TivuStreamList (Italian & international streams)
        • HLS playback: hls.js (MIT license)
        • UI components: MaterialDesignThemes.Wpf
        • WebView2: Microsoft Edge WebView2
        • Community & testing: CorvoBoys (corvoboys.org) & LinuxSat-Support.com

        For more information, visit the GitHub repository.";

            MessageBox.Show(help, LanguageManager.GetTranslation("Help"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CorvoBoysLink_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = "https://www.corvoboys.org", UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{LanguageManager.GetTranslation("Cannot open browser")}: {ex.Message}");
            }
        }

        private void LinuxSatLink_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = "https://www.linuxsat-support.com", UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{LanguageManager.GetTranslation("Cannot open browser")}: {ex.Message}");
            }
        }
    }
}