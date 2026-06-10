using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections;
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
using System.ComponentModel;
using System.Windows.Data;
using System.Text;
using Newtonsoft.Json.Linq;

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

        private void OnLanguageChanged()
        {
            ApplyLanguage();
        }

        private void ApplyLanguage()
        {
            // Toolbar buttons
            NewPlaylistBtn.Content = LanguageManager.GetTranslation("New Playlist");
            AddGroupBtn.Content = LanguageManager.GetTranslation("Add Group");
            RenameGroupBtn.Content = LanguageManager.GetTranslation("Rename Group");
            DeleteGroupBtn.Content = LanguageManager.GetTranslation("Delete Group");
            CheckUrlsBtn.Content = LanguageManager.GetTranslation("Check URLs");
            SaveStatusBtn.Content = LanguageManager.GetTranslation("Save Status");
            ImportJsonBtn.Content = LanguageManager.GetTranslation("Import Local");
            ImportJsonUrlBtn.Content = LanguageManager.GetTranslation("Import from URL");
            ExportJsonBtn.Content = LanguageManager.GetTranslation("Export JSON");
            ExportOkBtn.Content = LanguageManager.GetTranslation("Export OK");
            ExportFailedBtn.Content = LanguageManager.GetTranslation("Export KO");
            ExportFilteredM3uBtn.Content = LanguageManager.GetTranslation("Export Filtered M3U");
            ExportFilteredJsonBtn.Content = LanguageManager.GetTranslation("Export Filtered JSON");
            EnrichWithEpgBtn.Content = LanguageManager.GetTranslation("Enrich with EPG");
            FetchLogosBtn.Content = LanguageManager.GetTranslation("Fetch Logos");
            CheckDuplicatesBtn.Content = LanguageManager.GetTranslation("Check Duplicates");
            ResetOrderBtn.Content = LanguageManager.GetTranslation("Reset Order");
            SaveBtn.Content = LanguageManager.GetTranslation("Save as...");
            CloseBtn.Content = LanguageManager.GetTranslation("Exit");

            // Filter section
            FilterLabel.Text = LanguageManager.GetTranslation("FILTERS");
            ApplyFilterBtn.Content = LanguageManager.GetTranslation("Apply Filters");
            ClearFilterBtn.Content = LanguageManager.GetTranslation("Clear Filters");

            // Group headers inside filters (static, but you can translate if you want)
            var channelInfoLabel = FindName("ChannelInfoLabel") as TextBlock;
            if (channelInfoLabel != null) channelInfoLabel.Text = LanguageManager.GetTranslation("Channel Info");
            var epgFavLabel = FindName("EpgFavLabel") as TextBlock;
            if (epgFavLabel != null) epgFavLabel.Text = LanguageManager.GetTranslation("EPG & Favorites");
            var geoCountryLabel = FindName("GeoCountryLabel") as TextBlock;
            if (geoCountryLabel != null) geoCountryLabel.Text = LanguageManager.GetTranslation("Geo & Country");
            var advancedIdsLabel = FindName("AdvancedIdsLabel") as TextBlock;
            if (advancedIdsLabel != null) advancedIdsLabel.Text = LanguageManager.GetTranslation("Advanced IDs");
            var urlsStatusLabel = FindName("UrlsStatusLabel") as TextBlock;
            if (urlsStatusLabel != null) urlsStatusLabel.Text = LanguageManager.GetTranslation("URLs & Status");

            // Field labels inside filters (they are TextBlocks with x:Name)
            var nameFieldLabel = FindName("NameFieldLabel") as TextBlock;
            if (nameFieldLabel != null) nameFieldLabel.Text = LanguageManager.GetTranslation("Name");
            var urlFieldLabel = FindName("UrlFieldLabel") as TextBlock;
            if (urlFieldLabel != null) urlFieldLabel.Text = LanguageManager.GetTranslation("URL");
            var groupFieldLabel = FindName("GroupFieldLabel") as TextBlock;
            if (groupFieldLabel != null) groupFieldLabel.Text = LanguageManager.GetTranslation("Group");
            var logoFieldLabel = FindName("LogoFieldLabel") as TextBlock;
            if (logoFieldLabel != null) logoFieldLabel.Text = LanguageManager.GetTranslation("Logo URL");
            var tvgIdFieldLabel = FindName("TvgIdFieldLabel") as TextBlock;
            if (tvgIdFieldLabel != null) tvgIdFieldLabel.Text = LanguageManager.GetTranslation("TvgId");
            var countryFieldLabel = FindName("CountryFieldLabel") as TextBlock;
            if (countryFieldLabel != null) countryFieldLabel.Text = LanguageManager.GetTranslation("Country");
            var nanoidFieldLabel = FindName("NanoidFieldLabel") as TextBlock;
            if (nanoidFieldLabel != null) nanoidFieldLabel.Text = LanguageManager.GetTranslation("Nanoid");
            var languagesFieldLabel = FindName("LanguagesFieldLabel") as TextBlock;
            if (languagesFieldLabel != null) languagesFieldLabel.Text = LanguageManager.GetTranslation("Languages (comma)");
            var youtubeFieldLabel = FindName("YoutubeFieldLabel") as TextBlock;
            if (youtubeFieldLabel != null) youtubeFieldLabel.Text = LanguageManager.GetTranslation("Youtube URL");
            var streamFieldLabel = FindName("StreamFieldLabel") as TextBlock;
            if (streamFieldLabel != null) streamFieldLabel.Text = LanguageManager.GetTranslation("Stream URL");
            var statusFieldLabel = FindName("StatusFieldLabel") as TextBlock;
            if (statusFieldLabel != null) statusFieldLabel.Text = LanguageManager.GetTranslation("Status");

            // CheckBoxes
            FilterFavorite.Content = LanguageManager.GetTranslation("Favorite");
            FilterGeoBlocked.Content = LanguageManager.GetTranslation("GeoBlocked");

            // Window title
            Title = LanguageManager.GetTranslation("Playlist Editor");
        }

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
            LanguageManager.LanguageChanged += ApplyLanguage;
            ApplyLanguage();

            ChannelsGrid.SelectionChanged += (s, e) =>
            {
                int count = ChannelsGrid.SelectedItems.Count;
                SelectedCountText.Text = count > 0 ? $"{count} selected" : "";
            };
        }

        private void EditUrlsButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var channel = btn?.Tag as ChannelJson;
            if (channel == null) return;

            var dialog = new UrlListEditorWindow(channel.stream_urls ?? new List<string>());
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                channel.stream_urls = dialog.Urls;
                ChannelsGrid.Items.Refresh();
                // Aggiorna anche la visualizzazione nella cella
            }
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
                    string status;
                    if (allUrls.Count == 0)
                        status = "No URLs";
                    else if (okCount > 0)
                        status = $"{okCount}/{allUrls.Count} OK";
                    else
                        status = "FAIL";
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
                    string filePath = dialog.FileName;
                    var imported = new List<ChannelJson>();

                    using (var streamReader = new StreamReader(filePath, Encoding.UTF8))
                    using (var jsonReader = new JsonTextReader(streamReader))
                    {
                        // Support multiple JSON objects (either array or concatenated objects)
                        jsonReader.SupportMultipleContent = true;
                        while (jsonReader.Read())
                        {
                            if (jsonReader.TokenType == JsonToken.StartObject)
                            {
                                JObject obj = JObject.Load(jsonReader);
                                var channel = new ChannelJson
                                {
                                    name = obj["name"]?.ToString() ?? "",
                                    stream_urls = obj["stream_urls"]?.Type == JTokenType.Array 
                                        ? obj["stream_urls"].Select(t => t.ToString()).ToList() 
                                        : new List<string>(),
                                    logo_url = obj["logo_url"]?.ToString() ?? "",
                                    group = obj["group"]?.ToString() ?? "",
                                    tvg_id = obj["tvg_id"]?.ToString() ?? "",
                                    isFavorite = obj["isFavorite"]?.Value<bool>() ?? false,
                                    country = obj["country"]?.ToString() ?? "",
                                    youtube_urls = obj["youtube_urls"]?.Type == JTokenType.Array
                                        ? obj["youtube_urls"].Select(t => t.ToString()).ToList()
                                        : new List<string>(),
                                    nanoid = obj["nanoid"]?.ToString() ?? "",
                                    languages = obj["languages"]?.Type == JTokenType.Array
                                        ? obj["languages"].Select(t => t.ToString()).ToList()
                                        : new List<string>(),
                                    isGeoBlocked = obj["isGeoBlocked"]?.Value<bool>() ?? false
                                };
                                channel.stream_urls ??= new List<string>();
                                channel.youtube_urls ??= new List<string>();
                                channel.languages ??= new List<string>();
                                imported.Add(channel);
                            }
                        }
                    }

                    if (imported.Count == 0)
                        throw new Exception("No channels found in JSON file.");
                    Channels.Clear();
                    foreach (var ch in imported)
                        Channels.Add(ch);

                    ClearFilterBtn_Click(null, null);
                    MessageBox.Show(string.Format(LanguageManager.GetTranslation("Imported {0} channels from JSON."), imported.Count),
                                    LanguageManager.GetTranslation("Success"), MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"=== JSON IMPORT ERROR ===");
                    System.Diagnostics.Debug.WriteLine($"Message: {ex.Message}");
                    MessageBox.Show(string.Format(LanguageManager.GetTranslation("Import error: {0}"), ex.Message),
                                    LanguageManager.GetTranslation("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void ImportJsonUrlBtn_Click(object sender, RoutedEventArgs e)
        {
            string url = Microsoft.VisualBasic.Interaction.InputBox(
                LanguageManager.GetTranslation("Enter JSON URL:"),
                LanguageManager.GetTranslation("Import JSON from URL"),
                "");
            
            if (string.IsNullOrWhiteSpace(url)) return;

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "LiveGardenTVPlus");
                string jsonContent = await client.GetStringAsync(url);

                // Same logic as local import
                var imported = JsonConvert.DeserializeObject<List<ChannelJson>>(jsonContent);
                if (imported == null || imported.Count == 0)
                    throw new Exception("No channels found in JSON.");

                foreach (var ch in imported)
                {
                    ch.stream_urls ??= new List<string>();
                    ch.youtube_urls ??= new List<string>();
                    ch.languages ??= new List<string>();
                    ch.group ??= "";
                    ch.tvg_id ??= "";
                    ch.country ??= "";
                    ch.nanoid ??= "";
                    ch.logo_url ??= "";
                }

                foreach (var ch in imported)
                    Channels.Add(ch);
                
                ClearFilterBtn_Click(null, null);
                MessageBox.Show(string.Format(LanguageManager.GetTranslation("Imported {0} channels from URL."), imported.Count),
                                LanguageManager.GetTranslation("Success"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LanguageManager.GetTranslation("Import error: {0}"), ex.Message),
                                LanguageManager.GetTranslation("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void ChannelsGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var channel = ChannelsGrid.SelectedItem as ChannelJson;
            if (channel == null) return;
            OpenDetailsWindow(channel);
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var channel = button?.Tag as ChannelJson;
            if (channel == null) return;
            OpenDetailsWindow(channel);
        }

        private void OpenDetailsWindow(ChannelJson channel)
        {
            // Commit or cancel any pending edit
            ChannelsGrid.CommitEdit(DataGridEditingUnit.Row, true);
            // or ChannelsGrid.CancelEdit(DataGridEditingUnit.Row);

            int index = Channels.IndexOf(channel);
            var detailsWindow = new ChannelDetailsWindow(Channels, index);
            detailsWindow.Owner = this;
            if (detailsWindow.ShowDialog() == true)
            {
                ChannelsGrid.CommitEdit(DataGridEditingUnit.Row, true); // again to be safe
                ChannelsGrid.Items.Refresh();
            }
        }

        private void CheckDuplicatesBtn_Click(object sender, RoutedEventArgs e)
        {
            var urlToChannels = new Dictionary<string, List<string>>();
            foreach (var ch in Channels)
            {
                if (ch.stream_urls == null) continue;
                foreach (var url in ch.stream_urls)
                {
                    if (string.IsNullOrWhiteSpace(url)) continue;
                    if (!urlToChannels.ContainsKey(url))
                        urlToChannels[url] = new List<string>();
                    urlToChannels[url].Add(ch.name);
                }
            }

            var duplicates = urlToChannels.Where(kvp => kvp.Value.Count > 1).ToList();
            if (!duplicates.Any())
            {
                MessageBox.Show(LanguageManager.GetTranslation("No duplicate URLs found."),
                                LanguageManager.GetTranslation("Info"),
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var message = LanguageManager.GetTranslation("Duplicate URLs found:\n\n");
            foreach (var dup in duplicates)
            {
                message += $"{dup.Key}\n  → {string.Join(", ", dup.Value)}\n";
            }
            MessageBox.Show(message, LanguageManager.GetTranslation("Warning"),
                            MessageBoxButton.OK, MessageBoxImage.Warning);
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

        private void ChannelsGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            // Prevent default sorting for template columns
            e.Handled = true;

            // Determine which column to sort by
            string sortProperty = null;
            if (e.Column.Header?.ToString() == "URL (primary)")
                sortProperty = "PrimaryUrl";
            else if (e.Column.Header?.ToString() == "Logo")
                sortProperty = "logo_url";
            else if (e.Column.Header?.ToString() == "" || e.Column.Header?.ToString() == "✎") // icon column
                sortProperty = "name"; // or "logo_url" if you prefer
            else
            {
                // For standard columns, let WPF handle it
                e.Handled = false;
                return;
            }

            var view = CollectionViewSource.GetDefaultView(ChannelsGrid.ItemsSource);
            if (view == null) return;

            ListSortDirection direction = ListSortDirection.Ascending;
            if (view.SortDescriptions.Count > 0 && view.SortDescriptions[0].PropertyName == sortProperty)
                direction = view.SortDescriptions[0].Direction == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;

            using (view.DeferRefresh())
            {
                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(new SortDescription(sortProperty, direction));
            }

            // Update column header glyph (optional but nice)
            foreach (var col in ChannelsGrid.Columns)
            {
                col.SortDirection = null;
            }
            e.Column.SortDirection = direction;
        }

        private void ResetOrderBtn_Click(object sender, RoutedEventArgs e)
        {
            // Remove any sorting from the current view
            var view = CollectionViewSource.GetDefaultView(ChannelsGrid.ItemsSource);
            if (view != null)
            {
                view.SortDescriptions.Clear();
                // Remove sort direction arrows from column headers
                foreach (var col in ChannelsGrid.Columns)
                    col.SortDirection = null;
            }
            // Force visual refresh
            ChannelsGrid.Items.Refresh();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}