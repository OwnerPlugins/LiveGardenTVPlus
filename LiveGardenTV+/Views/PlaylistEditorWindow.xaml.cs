using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using LiveGardenTVPlus.Models;
using LiveGardenTVPlus.Services;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace LiveGardenTVPlus.Views
{
    public partial class PlaylistEditorWindow : Window
    {
        public ObservableCollection<ChannelJson> Channels { get; set; }
        public bool IsSaved { get; private set; }
        public string SavedFilePath { get; private set; }
        private EpgService _epgService; 

        public PlaylistEditorWindow(List<Channel> channels, EpgService epgService = null)
        {
            InitializeComponent();
            DataContext = this;
            _epgService = epgService;
            LanguageManager.LanguageChanged += OnLanguageChanged;
            ApplyLanguage();

            // Convert Channel list to ChannelJson with safe initialization
            var editable = channels.Select(c => new ChannelJson
            {
                name = c.Name,
                stream_urls = new List<string> { c.Url },
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

        private void OnLanguageChanged() => ApplyLanguage();

        private void ApplyLanguage()
        {
            AddGroupBtn.Content = LanguageManager.GetTranslation("Add Group");
            NewPlaylistBtn.Content = LanguageManager.GetTranslation("New Playlist");
            RenameGroupBtn.Content = LanguageManager.GetTranslation("Rename Group");
            DeleteGroupBtn.Content = LanguageManager.GetTranslation("Delete Group");
            CheckUrlsBtn.Content = LanguageManager.GetTranslation("Check URLs");
            SaveStatusBtn.Content = LanguageManager.GetTranslation("Save Status");
            ImportJsonBtn.Content = LanguageManager.GetTranslation("Import JSON");
            ExportJsonBtn.Content = LanguageManager.GetTranslation("Export JSON");
            ExportOkBtn.Content = LanguageManager.GetTranslation("Export OK");
            ExportFailedBtn.Content = LanguageManager.GetTranslation("Export Failed");
            ExportFilteredM3uBtn.Content = LanguageManager.GetTranslation("Export Filtered M3U");
            ExportFilteredJsonBtn.Content = LanguageManager.GetTranslation("Export Filtered JSON");
            EnrichWithEpgBtn.Content = LanguageManager.GetTranslation("Enrich with EPG");
            SaveBtn.Content = LanguageManager.GetTranslation("Save As M3U");
            CloseBtn.Content = LanguageManager.GetTranslation("Close");
            FilterLabel.Text = LanguageManager.GetTranslation("FILTERS");
            ApplyFilterBtn.Content = LanguageManager.GetTranslation("Apply");
            ClearFilterBtn.Content = LanguageManager.GetTranslation("Clear");
        }

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

        // -------------------- Group Management with safety --------------------
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
                // Reset all filters to ensure the new channel is visible
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
                    MessageBox.Show("Select a channel from the group you want to rename.",
                                    "No selection", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    MessageBox.Show("Select a channel from the group you want to delete.",
                                    "No selection", MessageBoxButton.OK, MessageBoxImage.Information);
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
                MessageBox.Show($"Deleted {toRemove.Count} channel(s) from group '{groupToDelete}'.",
                                "Group deleted", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting group: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // -------------------- URL Check (unchanged but safe) --------------------
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
            // Refresh only the affected row
            ChannelsGrid.Items.Refresh();
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

        private async void EnrichWithEpgBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_epgService == null)
            {
                MessageBox.Show(LanguageManager.GetTranslation("EPG service is not available. Load a playlist with EPG data first."),
                                LanguageManager.GetTranslation("Cannot Enrich"), MessageBoxButton.OK, MessageBoxImage.Warning);
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
                // Open Save As dialog with multiple formats
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "M3U files|*.m3u|JSON files|*.json|CSV files|*.csv",
                    DefaultExt = ".m3u",
                    FileName = "enriched_playlist"
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

                    MessageBox.Show($"Enriched playlist saved to {filePath}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    SavedFilePath = filePath;
                    IsSaved = true;
                }
            }
        }

        // -------------------- Export methods --------------------
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
                        writer.WriteLine($"#EXTINF:-1 group-title=\"{ch.group}\" tvg-logo=\"{ch.logo_url}\" tvg-id=\"{ch.tvg_id}\",{ch.name}");
                        writer.WriteLine(url);
                    }
                }
                MessageBox.Show($"Exported {channels.Count} channels to {dialog.FileName}", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
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
                        writer.WriteLine($"\"{ch.name}\",\"{ch.group}\",\"{url}\",{ch.UrlStatus}");
                    }
                }
                MessageBox.Show($"Status of {visibleChannels.Count()} channels saved to {dialog.FileName}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // -------------------- JSON Import/Export --------------------
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

                    // Ensure no null collections
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

        // Helper method for M3U export (reuse existing logic)
        private void ExportToM3u(string filePath, List<ChannelJson> channels)
        {
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("#EXTM3U");
                foreach (var ch in channels)
                {
                    string url = ch.stream_urls?.FirstOrDefault() ?? "";
                    writer.WriteLine($"#EXTINF:-1 group-title=\"{ch.group}\" tvg-logo=\"{ch.logo_url}\" tvg-id=\"{ch.tvg_id}\",{ch.name}");
                    writer.WriteLine(url);
                }
            }
        }

        // Helper method for JSON export
        private void ExportToJson(string filePath, List<ChannelJson> channels)
        {
            string json = JsonConvert.SerializeObject(channels, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        // Helper method for CSV export (simple format: name, group, url, tvg_id, logo)
        private void ExportToCsv(string filePath, List<ChannelJson> channels)
        {
            using (var writer = new StreamWriter(filePath))
            {
                // Write header
                writer.WriteLine("\"Name\",\"Group\",\"URL\",\"TvgId\",\"Logo\",\"Favorite\",\"Country\",\"GeoBlocked\",\"Languages\",\"Status\"");
                
                foreach (var ch in channels)
                {
                    string url = ch.stream_urls?.FirstOrDefault() ?? "";
                    string languages = ch.languages != null ? string.Join(";", ch.languages) : "";
                    writer.WriteLine($"\"{EscapeCsv(ch.name)}\",\"{EscapeCsv(ch.group)}\",\"{EscapeCsv(url)}\",\"{EscapeCsv(ch.tvg_id)}\",\"{EscapeCsv(ch.logo_url)}\",{ch.isFavorite},\"{EscapeCsv(ch.country)}\",{ch.isGeoBlocked},\"{EscapeCsv(languages)}\",\"{EscapeCsv(ch.UrlStatus)}\"");
                }
            }
        }

        // Helper to escape double quotes in CSV fields
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