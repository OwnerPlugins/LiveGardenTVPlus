using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LiveGardenTVPlus.Models;
using LiveGardenTVPlus.Services;
using Newtonsoft.Json;
using System.Windows.Input;
using System.Windows.Threading;

namespace LiveGardenTVPlus.Views
{
    public partial class PlaylistEditorWindow : Window
    {
        public ObservableCollection<ChannelJson> Channels { get; set; }
        public bool IsSaved { get; private set; }
        public string SavedFilePath { get; private set; }
        private EpgService _epgService;
        private List<LogoInfo> _cachedLogos;
        private bool _isFetchingLogos = false;
        private System.Windows.Threading.DispatcherTimer _progressTimer;
        private DateTime _logoDownloadStartTime;

        private void NewPlaylistBtn_Click(object sender, RoutedEventArgs e)
        {
            if (Channels.Count > 0)
            {
                var result = MessageBox.Show("This will clear the current playlist. Continue?", "New Playlist",
                                              MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;
            }

            Channels.Clear();
            ClearFilterBtn_Click(null, null);
            UpdateFilteredCount();
            IsSaved = false;
            SavedFilePath = null;
            MessageBox.Show("New empty playlist created. Use 'Add Group' to create groups and then edit cells to add channels.",
                            "Playlist Ready", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public PlaylistEditorWindow(List<Channel> channels, EpgService epgService = null)
        {
            InitializeComponent();
            DataContext = this;
            _epgService = epgService;

            // Convert Channel list to ChannelJson with safe initialization
            var editable = channels.Select(c => new ChannelJson
            {
                name = c.Name,
                stream_urls = string.IsNullOrEmpty(c.Url) ? new List<string>() : new List<string> { c.Url },
                logo_url = c.Logo,
                group = c.Group,
                tvg_id = c.TvgId,
                isFavorite = c.IsFavorite,
                country = "",
                youtube_urls = new List<string>(),
                nanoid = "",
                languages = new List<string>(),
                isGeoBlocked = false,
                UrlStatus = ""
            }).ToList();

            Channels = new ObservableCollection<ChannelJson>(editable);
            ChannelsGrid.ItemsSource = Channels;
            UpdateFilteredCount();
            IsSaved = false;
            SavedFilePath = null;

            ChannelsGrid.SelectionChanged += (s, e) =>
            {
                int count = ChannelsGrid.SelectedItems.Count;
                SelectedCountText.Text = count > 0 ? $"{count} selected" : "";
            };
        }

        // ------------------------------------------------------------------
        // Filtering
        // ------------------------------------------------------------------
        private void UpdateFilteredCount()
        {
            var visible = ChannelsGrid.ItemsSource as IEnumerable<ChannelJson>;
            int visibleCount = visible?.Count() ?? 0;
            FilteredCountText.Text = $"Showing {visibleCount} of {Channels.Count} channels";
        }

        private void ApplyFilter()
        {
            try
            {
                if (Channels == null) return;
                var filtered = Channels.AsEnumerable();

                if (!string.IsNullOrEmpty(FilterName.Text))
                    filtered = filtered.Where(c => c.name?.IndexOf(FilterName.Text, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!string.IsNullOrEmpty(FilterUrl.Text))
                    filtered = filtered.Where(c => c.stream_urls != null && c.stream_urls.Any(u => u.IndexOf(FilterUrl.Text, StringComparison.OrdinalIgnoreCase) >= 0));
                if (!string.IsNullOrEmpty(FilterGroup.Text))
                    filtered = filtered.Where(c => c.group?.IndexOf(FilterGroup.Text, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!string.IsNullOrEmpty(FilterLogo.Text))
                    filtered = filtered.Where(c => c.logo_url?.IndexOf(FilterLogo.Text, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!string.IsNullOrEmpty(FilterTvgId.Text))
                    filtered = filtered.Where(c => c.tvg_id?.IndexOf(FilterTvgId.Text, StringComparison.OrdinalIgnoreCase) >= 0);
                if (FilterFavorite.IsChecked == true)
                    filtered = filtered.Where(c => c.isFavorite);
                if (!string.IsNullOrEmpty(FilterCountry.Text))
                    filtered = filtered.Where(c => c.country?.IndexOf(FilterCountry.Text, StringComparison.OrdinalIgnoreCase) >= 0);
                if (FilterGeoBlocked.IsChecked == true)
                    filtered = filtered.Where(c => c.isGeoBlocked);
                if (!string.IsNullOrEmpty(FilterNanoid.Text))
                    filtered = filtered.Where(c => c.nanoid?.IndexOf(FilterNanoid.Text, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!string.IsNullOrEmpty(FilterLanguages.Text))
                {
                    var langList = FilterLanguages.Text.Split(',').Select(l => l.Trim().ToLower());
                    filtered = filtered.Where(c => c.languages != null && c.languages.Any(l => langList.Contains(l.ToLower())));
                }
                if (!string.IsNullOrEmpty(FilterYoutube.Text))
                    filtered = filtered.Where(c => c.youtube_urls != null && c.youtube_urls.Any(u => u.IndexOf(FilterYoutube.Text, StringComparison.OrdinalIgnoreCase) >= 0));
                if (!string.IsNullOrEmpty(FilterStream.Text))
                    filtered = filtered.Where(c => c.stream_urls != null && c.stream_urls.Any(u => u.IndexOf(FilterStream.Text, StringComparison.OrdinalIgnoreCase) >= 0));
                if (!string.IsNullOrEmpty(FilterStatus.Text))
                    filtered = filtered.Where(c => c.UrlStatus?.IndexOf(FilterStatus.Text, StringComparison.OrdinalIgnoreCase) >= 0);

                var filteredList = filtered.ToList();
                ChannelsGrid.ItemsSource = filteredList;
                FilteredCountText.Text = $"Showing {filteredList.Count} of {Channels.Count} channels";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Filter error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilterBtn_Click(object sender, RoutedEventArgs e) => ApplyFilter();

        private void ClearFilterBtn_Click(object sender, RoutedEventArgs e)
        {
            FilterName.Text = FilterUrl.Text = FilterGroup.Text = FilterLogo.Text = FilterTvgId.Text =
            FilterCountry.Text = FilterNanoid.Text = FilterLanguages.Text = FilterYoutube.Text =
            FilterStream.Text = FilterStatus.Text = "";
            FilterFavorite.IsChecked = FilterGeoBlocked.IsChecked = null;
            ApplyFilter();
        }

        // ------------------------------------------------------------------
        // Group management
        // ------------------------------------------------------------------
        private void AddGroupBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string newGroupName = Microsoft.VisualBasic.Interaction.InputBox("Enter new group name:", "Add Group", "");
                if (string.IsNullOrWhiteSpace(newGroupName)) return;

                var newChannel = new ChannelJson
                {
                    name = "New channel",
                    stream_urls = new List<string>(),
                    logo_url = "",
                    group = newGroupName,
                    tvg_id = "",
                    isFavorite = false,
                    country = "",
                    youtube_urls = new List<string>(),
                    nanoid = "",
                    languages = new List<string>(),
                    isGeoBlocked = false,
                    UrlStatus = ""
                };
                Channels.Add(newChannel);
                ClearFilterBtn_Click(null, null);
                MessageBox.Show($"Group '{newGroupName}' created with an empty channel.", "Group added", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding group: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RenameGroupBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selected = ChannelsGrid.SelectedItem as ChannelJson;
                if (selected == null)
                {
                    MessageBox.Show("Select a channel from the group you want to rename.", "No selection", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                string oldGroup = selected.group ?? "";
                string newGroup = Microsoft.VisualBasic.Interaction.InputBox($"Rename group '{oldGroup}' to:", "Rename Group", oldGroup);
                if (string.IsNullOrEmpty(newGroup) || newGroup == oldGroup) return;

                foreach (var ch in Channels.Where(c => (c.group ?? "") == oldGroup))
                    ch.group = newGroup;

                ChannelsGrid.Items.Refresh();
                UpdateFilteredCount();
                MessageBox.Show($"Group '{oldGroup}' renamed to '{newGroup}'.", "Rename done", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error renaming group: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteGroupBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selected = ChannelsGrid.SelectedItem as ChannelJson;
                if (selected == null)
                {
                    MessageBox.Show("Select a channel from the group you want to delete.", "No selection", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                string groupToDelete = selected.group ?? "";
                if (MessageBox.Show($"Delete ALL channels in group '{groupToDelete}'? This cannot be undone.",
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;

                var toRemove = Channels.Where(c => (c.group ?? "") == groupToDelete).ToList();
                foreach (var ch in toRemove)
                    Channels.Remove(ch);

                UpdateFilteredCount();
                MessageBox.Show($"Deleted {toRemove.Count} channel(s) from group '{groupToDelete}'.", "Group deleted", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting group: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ------------------------------------------------------------------
        // URL check (only on visible/filtered channels)
        // ------------------------------------------------------------------
        private async void CheckUrlsBtn_Click(object sender, RoutedEventArgs e)
        {
            var visibleChannels = ChannelsGrid.ItemsSource as IEnumerable<ChannelJson>;
            if (visibleChannels == null || !visibleChannels.Any())
            {
                MessageBox.Show("No channels to check (filters are active but no results).", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            CheckUrlsBtn.IsEnabled = false;
            CheckProgressBar.Visibility = Visibility.Visible;
            var progress = new Progress<KeyValuePair<ChannelJson, string>>(UpdateUrlStatus);
            await Task.Run(() => CheckAllUrls(visibleChannels.ToList(), progress));
            CheckProgressBar.Visibility = Visibility.Collapsed;
            CheckUrlsBtn.IsEnabled = true;
            MessageBox.Show($"URL check completed on {visibleChannels.Count()} channels.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UpdateUrlStatus(KeyValuePair<ChannelJson, string> result)
        {
            result.Key.UrlStatus = result.Value;
            Dispatcher.Invoke(() => ChannelsGrid.Items.Refresh());
        }

        private void CheckAllUrls(List<ChannelJson> channelsToCheck, IProgress<KeyValuePair<ChannelJson, string>> progress)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(5);
                foreach (var channel in channelsToCheck)
                {
                    var allUrls = new List<string>();
                    if (channel.stream_urls != null) allUrls.AddRange(channel.stream_urls);
                    if (channel.youtube_urls != null) allUrls.AddRange(channel.youtube_urls);
                    if (allUrls.Count == 0)
                    {
                        progress.Report(new KeyValuePair<ChannelJson, string>(channel, "No URLs"));
                        continue;
                    }
                    int okCount = 0;
                    foreach (var url in allUrls)
                    {
                        bool isOk = false;
                        try
                        {
                            var request = new HttpRequestMessage(HttpMethod.Get, url);
                            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
                            var response = client.Send(request);
                            isOk = response.IsSuccessStatusCode;
                            if (!isOk)
                            {
                                var fullRequest = new HttpRequestMessage(HttpMethod.Get, url);
                                var fullResponse = client.Send(fullRequest, HttpCompletionOption.ResponseHeadersRead);
                                isOk = fullResponse.IsSuccessStatusCode;
                            }
                        }
                        catch { isOk = false; }
                        if (isOk) okCount++;
                    }
                    string status = $"{okCount}/{allUrls.Count} OK";
                    progress.Report(new KeyValuePair<ChannelJson, string>(channel, status));
                }
            }
        }

        private void SaveStatusBtn_Click(object sender, RoutedEventArgs e)
        {
            var visibleChannels = ChannelsGrid.ItemsSource as IEnumerable<ChannelJson>;
            if (visibleChannels == null || !visibleChannels.Any())
            {
                MessageBox.Show("No channels to export (filters active but no results).", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV files|*.csv",
                DefaultExt = ".csv",
                FileName = "channel_status.csv"
            };
            if (dialog.ShowDialog() == true)
            {
                using (var writer = new StreamWriter(dialog.FileName))
                {
                    writer.WriteLine("Name,Group,URL,Status");
                    foreach (var ch in visibleChannels)
                    {
                        string url = ch.stream_urls?.FirstOrDefault() ?? "";
                        if (string.IsNullOrEmpty(url)) continue;
                        writer.WriteLine($"\"{ch.name}\",\"{ch.group}\",\"{url}\",{ch.UrlStatus}");
                    }
                }
                MessageBox.Show($"Status of {visibleChannels.Count()} channels saved to {dialog.FileName}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // ------------------------------------------------------------------
        // Export helpers (M3U, JSON, CSV)
        // ------------------------------------------------------------------
        private void ExportOkBtn_Click(object sender, RoutedEventArgs e)
        {
            var visibleChannels = ChannelsGrid.ItemsSource as IEnumerable<ChannelJson>;
            if (visibleChannels == null || !visibleChannels.Any())
            {
                MessageBox.Show("No channels to export.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var working = visibleChannels.Where(c => c.UrlStatus?.StartsWith("OK") == true).ToList();
            if (working.Count == 0)
            {
                MessageBox.Show("No working channels to export.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            ExportChannelsToM3u(working, "working_channels.m3u");
        }

        private void ExportFailedBtn_Click(object sender, RoutedEventArgs e)
        {
            var visibleChannels = ChannelsGrid.ItemsSource as IEnumerable<ChannelJson>;
            if (visibleChannels == null || !visibleChannels.Any())
            {
                MessageBox.Show("No channels to export.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var failed = visibleChannels.Where(c => c.UrlStatus?.StartsWith("FAIL") == true).ToList();
            if (failed.Count == 0)
            {
                MessageBox.Show("No failed channels to export.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            ExportChannelsToM3u(failed, "failed_channels.m3u");
        }

        private void ExportFilteredM3uBtn_Click(object sender, RoutedEventArgs e)
        {
            var filtered = ChannelsGrid.ItemsSource as IEnumerable<ChannelJson>;
            if (filtered == null || !filtered.Any())
            {
                MessageBox.Show("No channels to export.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            ExportChannelsToM3u(filtered.ToList(), "filtered_playlist.m3u");
        }

        private void ExportFilteredJsonBtn_Click(object sender, RoutedEventArgs e)
        {
            var filtered = ChannelsGrid.ItemsSource as IEnumerable<ChannelJson>;
            if (filtered == null || !filtered.Any())
            {
                MessageBox.Show("No channels to export.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON files|*.json",
                DefaultExt = ".json",
                FileName = "filtered_export.json"
            };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    string json = JsonConvert.SerializeObject(filtered, Formatting.Indented);
                    File.WriteAllText(dialog.FileName, json);
                    MessageBox.Show($"Exported {filtered.Count()} channels to JSON.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportChannelsToM3u(List<ChannelJson> channels, string defaultFileName)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "M3U files|*.m3u",
                DefaultExt = ".m3u",
                FileName = defaultFileName
            };
            if (dialog.ShowDialog() == true)
            {
                using (var writer = new StreamWriter(dialog.FileName))
                {
                    writer.WriteLine("#EXTM3U");
                    foreach (var ch in channels)
                    {
                        string url = ch.stream_urls?.FirstOrDefault() ?? "";
                        if (string.IsNullOrEmpty(url)) continue;
                        string logoAttr = string.IsNullOrEmpty(ch.logo_url) ? "" : $" tvg-logo=\"{ch.logo_url}\"";
                        string tvgIdAttr = string.IsNullOrEmpty(ch.tvg_id) ? "" : $" tvg-id=\"{ch.tvg_id}\"";
                        writer.WriteLine($"#EXTINF:-1 group-title=\"{ch.group}\"{logoAttr}{tvgIdAttr},{ch.name}");
                        writer.WriteLine(url);
                    }
                }
                MessageBox.Show($"Exported {channels.Count} channels to {dialog.FileName}", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // ------------------------------------------------------------------
        // JSON Import / Export (full playlist)
        // ------------------------------------------------------------------
        private async void ImportJsonBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files|*.json",
                DefaultExt = ".json"
            };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    string json = await File.ReadAllTextAsync(dialog.FileName);
                    var imported = JsonConvert.DeserializeObject<List<ChannelJson>>(json);
                    if (imported == null || imported.Count == 0)
                        throw new Exception("No channels found in JSON file.");

                    foreach (var ch in imported)
                    {
                        if (ch.stream_urls == null) ch.stream_urls = new List<string>();
                        if (ch.youtube_urls == null) ch.youtube_urls = new List<string>();
                        if (ch.languages == null) ch.languages = new List<string>();
                    }

                    Channels.Clear();
                    foreach (var ch in imported)
                        Channels.Add(ch);
                    ClearFilterBtn_Click(null, null);
                    MessageBox.Show($"Imported {Channels.Count} channels from JSON.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Import error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportJsonBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON files|*.json",
                DefaultExt = ".json",
                FileName = "playlist_export.json"
            };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    string json = JsonConvert.SerializeObject(Channels, Formatting.Indented);
                    File.WriteAllText(dialog.FileName, json);
                    MessageBox.Show($"Exported {Channels.Count} channels to JSON.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ------------------------------------------------------------------
        // Enrich with EPG (fuzzy matching)
        // ------------------------------------------------------------------
        private void EnrichWithEpgBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_epgService == null)
            {
                MessageBox.Show("EPG service is not available. Load a playlist with EPG data first.",
                                "Cannot Enrich", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int enrichedCount = 0;
            int alreadyHadCount = 0;

            foreach (var ch in Channels)
            {
                if (!string.IsNullOrEmpty(ch.tvg_id))
                {
                    alreadyHadCount++;
                    continue;
                }

                string epgId = _epgService.GetMappedEpgId(ch.name);
                if (!string.IsNullOrEmpty(epgId))
                {
                    ch.tvg_id = epgId;
                    enrichedCount++;
                }
            }

            ChannelsGrid.Items.Refresh();

            string message = $"Enrichment completed.\n" +
                             $"- Channels already with tvg-id: {alreadyHadCount}\n" +
                             $"- Newly enriched with EPG id: {enrichedCount}\n" +
                             $"- Remaining without EPG: {Channels.Count - alreadyHadCount - enrichedCount}\n\n" +
                             "Do you want to save the enriched playlist now?";

            var result = MessageBox.Show(message, "Enrich M3U with EPG", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                SaveBtn_Click(sender, e);
            }
        }

        // ------------------------------------------------------------------
        // Fetch Logos from remote index (logos.txt)
        // ------------------------------------------------------------------
        private async void FetchLogosBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isFetchingLogos) return;
            _isFetchingLogos = true;
            this.Cursor = Cursors.Wait;
            FetchLogosBtn.IsEnabled = false;
            StartLogoProgress();

            try
            {
                var prefs = UserPreferences.Load();
                var logoService = new LogoService();
                string indexUrl = "https://raw.githubusercontent.com/OwnerPlugins/logos/main/txt/logos.txt";
                bool loaded = await logoService.LoadLogosFromIndex(indexUrl, prefs.LogosSubFolder);
                if (!loaded)
                {
                    MessageBox.Show("Failed to load logos index.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                StopLogoProgress();
                CheckProgressBar.Visibility = Visibility.Visible;
                CheckProgressBar.IsIndeterminate = false;
                CheckProgressBar.Minimum = 0;
                CheckProgressBar.Maximum = Channels.Count;
                CheckProgressBar.Value = 0;

                int assigned = 0;
                int index = 0;
                foreach (var ch in Channels)
                {
                    index++;
                    CheckProgressBar.Value = index;
                    CheckProgressBar.ToolTip = $"Processing {index}/{Channels.Count}...";

                    if (!string.IsNullOrEmpty(ch.logo_url)) continue;
                    string logoUrl = logoService.FindBestMatch(ch.name, ch.tvg_id);
                    if (!string.IsNullOrEmpty(logoUrl) && !logoUrl.EndsWith("/.png") && logoUrl.Contains(".png"))
                    {
                        ch.logo_url = logoUrl;
                        assigned++;
                    }

                    // Allow UI to update (small delay if needed, but not necessary)
                    // await Task.Delay(1);
                }

                ChannelsGrid.Items.Refresh();
                MessageBox.Show($"Assigned {assigned} logos out of {Channels.Count} channels.", "Logos Fetch", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            finally
            {
                StopLogoProgress();
                this.Cursor = Cursors.Arrow;
                FetchLogosBtn.IsEnabled = true;
                _isFetchingLogos = false;
            }
        }

        private async void PickLogo_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var channel = btn?.Tag as ChannelJson;
            if (channel == null) return;

            if (_isFetchingLogos) return;
            _isFetchingLogos = true;
            this.Cursor = System.Windows.Input.Cursors.Wait;
            try
            {
                if (_cachedLogos == null)
                {
                    var prefs = UserPreferences.Load();
                    var logoService = new LogoService();
                    string indexUrl = "https://raw.githubusercontent.com/OwnerPlugins/logos/main/txt/logos.txt";
                    bool loaded = await logoService.LoadLogosFromIndex(indexUrl, prefs.LogosSubFolder);
                    if (!loaded) throw new Exception("Failed to load logos.");
                    _cachedLogos = logoService.GetAllLogos();
                }

                if (_cachedLogos == null || _cachedLogos.Count == 0)
                {
                    MessageBox.Show("No logos available.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var picker = new LogoPickerWindow(_cachedLogos);
                picker.Owner = this;
                if (picker.ShowDialog() == true)
                {
                    channel.logo_url = picker.SelectedLogoUrl;
                    ChannelsGrid.Items.Refresh();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading logos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.Cursor = System.Windows.Input.Cursors.Arrow;
                _isFetchingLogos = false;
            }
        }

        private void StartLogoProgress()
        {
            CheckProgressBar.Visibility = Visibility.Visible;
            CheckProgressBar.IsIndeterminate = true;
            CheckProgressBar.ToolTip = "Downloading logos (first time only)…";
        }

        private void StopLogoProgress()
        {
            CheckProgressBar.Visibility = Visibility.Collapsed;
            CheckProgressBar.IsIndeterminate = false;
            CheckProgressBar.Value = 0;
            CheckProgressBar.ToolTip = null;
        }

        // ------------------------------------------------------------------
        // Save As (multi-format) & Close
        // ------------------------------------------------------------------
        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "M3U files|*.m3u|JSON files|*.json|CSV files|*.csv",
                DefaultExt = ".m3u",
                FileName = "playlist_export"
            };

            if (dialog.ShowDialog() == true)
            {
                string filePath = dialog.FileName;
                string extension = System.IO.Path.GetExtension(filePath).ToLower();

                switch (extension)
                {
                    case ".m3u":
                        ExportToM3u(filePath, Channels.ToList());
                        break;
                    case ".json":
                        ExportToJson(filePath, Channels.ToList());
                        break;
                    case ".csv":
                        ExportToCsv(filePath, Channels.ToList());
                        break;
                    default:
                        MessageBox.Show("Unsupported file format.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                }

                MessageBox.Show($"Playlist saved to {filePath}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                SavedFilePath = filePath;
                IsSaved = true;
            }
        }

        private void ExportToM3u(string filePath, List<ChannelJson> channels)
        {
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("#EXTM3U");
                foreach (var ch in channels)
                {
                    string url = ch.stream_urls?.FirstOrDefault() ?? "";
                    if (string.IsNullOrEmpty(url)) continue;
                    string logoAttr = string.IsNullOrEmpty(ch.logo_url) ? "" : $" tvg-logo=\"{ch.logo_url}\"";
                    string tvgIdAttr = string.IsNullOrEmpty(ch.tvg_id) ? "" : $" tvg-id=\"{ch.tvg_id}\"";
                    writer.WriteLine($"#EXTINF:-1 group-title=\"{ch.group}\"{logoAttr}{tvgIdAttr},{ch.name}");
                    writer.WriteLine(url);
                }
            }
        }

        private void ExportToJson(string filePath, List<ChannelJson> channels)
        {
            string json = JsonConvert.SerializeObject(channels, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        private void ExportToCsv(string filePath, List<ChannelJson> channels)
        {
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("\"Name\",\"Group\",\"URL\",\"TvgId\",\"Logo\",\"Favorite\",\"Country\",\"GeoBlocked\",\"Languages\",\"Status\"");
                foreach (var ch in channels)
                {
                    string url = ch.stream_urls?.FirstOrDefault() ?? "";
                    if (string.IsNullOrEmpty(url)) continue;
                    string languages = ch.languages != null ? string.Join(";", ch.languages) : "";
                    writer.WriteLine($"\"{EscapeCsv(ch.name)}\",\"{EscapeCsv(ch.group)}\",\"{EscapeCsv(url)}\",\"{EscapeCsv(ch.tvg_id)}\",\"{EscapeCsv(ch.logo_url)}\",{ch.isFavorite},\"{EscapeCsv(ch.country)}\",{ch.isGeoBlocked},\"{EscapeCsv(languages)}\",\"{EscapeCsv(ch.UrlStatus)}\"");
                }
            }
        }

        private string EscapeCsv(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            return field.Replace("\"", "\"\"");
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}