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

namespace LiveGardenTVPlus.Views
{
    public partial class PlaylistEditorWindow : Window
    {
        public ObservableCollection<ChannelJson> Channels { get; set; }
        public bool IsSaved { get; private set; }
        public string SavedFilePath { get; private set; }

        public PlaylistEditorWindow(List<Channel> channels)
        {
            InitializeComponent();
            DataContext = this;

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
                isGeoBlocked = false
            }).ToList();

            Channels = new ObservableCollection<ChannelJson>(editable);
            ChannelsGrid.ItemsSource = Channels;
            FilteredCountText.Text = $"Showing {Channels.Count} of {Channels.Count} channels";
            IsSaved = false;
            SavedFilePath = null;
        }

        private void ApplyFilter()
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

        private void ApplyFilterBtn_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
        }

        private void ClearFilterBtn_Click(object sender, RoutedEventArgs e)
        {
            FilterName.Text = "";
            FilterUrl.Text = "";
            FilterGroup.Text = "";
            FilterLogo.Text = "";
            FilterTvgId.Text = "";
            FilterCountry.Text = "";
            FilterNanoid.Text = "";
            FilterLanguages.Text = "";
            FilterYoutube.Text = "";
            FilterStream.Text = "";
            FilterStatus.Text = "";
            FilterFavorite.IsChecked = null;
            FilterGeoBlocked.IsChecked = null;
            ApplyFilter();
        }

        // -------------------- Group Management --------------------
        private void AddGroupBtn_Click(object sender, RoutedEventArgs e)
        {
            string groupName = Microsoft.VisualBasic.Interaction.InputBox("Enter new group name:", "Add Group", "");
            if (!string.IsNullOrEmpty(groupName))
            {
                foreach (var ch in Channels)
                    if (string.IsNullOrEmpty(ch.group)) ch.group = groupName;
                ChannelsGrid.Items.Refresh();
            }
        }

        private void RenameGroupBtn_Click(object sender, RoutedEventArgs e)
        {
            var selected = ChannelsGrid.SelectedItem as ChannelJson;
            if (selected == null)
            {
                MessageBox.Show("Select a channel from the group you want to rename.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string oldGroup = selected.group;
            string newGroup = Microsoft.VisualBasic.Interaction.InputBox($"Rename group '{oldGroup}' to:", "Rename Group", oldGroup);
            if (!string.IsNullOrEmpty(newGroup) && newGroup != oldGroup)
            {
                foreach (var ch in Channels.Where(c => c.group == oldGroup))
                    ch.group = newGroup;
                ChannelsGrid.Items.Refresh();
            }
        }

        private void DeleteGroupBtn_Click(object sender, RoutedEventArgs e)
        {
            var selected = ChannelsGrid.SelectedItem as ChannelJson;
            if (selected == null)
            {
                MessageBox.Show("Select a channel from the group you want to delete.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string groupToDelete = selected.group;
            if (MessageBox.Show($"Delete all channels in group '{groupToDelete}'? This cannot be undone.", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                var toRemove = Channels.Where(c => c.group == groupToDelete).ToList();
                foreach (var ch in toRemove)
                    Channels.Remove(ch);
            }
        }

        // -------------------- URL Check --------------------
        private async void CheckUrlsBtn_Click(object sender, RoutedEventArgs e)
        {
            CheckUrlsBtn.IsEnabled = false;
            var progress = new Progress<KeyValuePair<ChannelJson, string>>(UpdateUrlStatus);
            await Task.Run(() => CheckAllUrls(progress));
            CheckUrlsBtn.IsEnabled = true;
            MessageBox.Show("URL check completed.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UpdateUrlStatus(KeyValuePair<ChannelJson, string> result)
        {
            result.Key.UrlStatus = result.Value;
            ChannelsGrid.Items.Refresh();
        }

        private void CheckAllUrls(IProgress<KeyValuePair<ChannelJson, string>> progress)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(5);
                foreach (var channel in Channels)
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

        private void ExportOkBtn_Click(object sender, RoutedEventArgs e)
        {
            var working = Channels.Where(c => c.UrlStatus?.StartsWith("OK") == true).ToList();
            if (working.Count == 0)
            {
                MessageBox.Show("No working channels to export.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            ExportChannelsToM3u(working, "working_channels.m3u");
        }

        private void ExportFailedBtn_Click(object sender, RoutedEventArgs e)
        {
            var failed = Channels.Where(c => c.UrlStatus?.StartsWith("FAIL") == true).ToList();
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
                    foreach (var ch in Channels)
                    {
                        string url = ch.stream_urls?.FirstOrDefault() ?? "";
                        writer.WriteLine($"\"{ch.name}\",\"{ch.group}\",\"{url}\",{ch.UrlStatus}");
                    }
                }
                MessageBox.Show($"Status saved to {dialog.FileName}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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
                Filter = "M3U files|*.m3u",
                DefaultExt = ".m3u",
                FileName = "playlist_edited.m3u"
            };
            if (dialog.ShowDialog() == true)
            {
                using (var writer = new StreamWriter(dialog.FileName))
                {
                    writer.WriteLine("#EXTM3U");
                    foreach (var ch in Channels)
                    {
                        string url = ch.stream_urls?.FirstOrDefault() ?? "";
                        writer.WriteLine($"#EXTINF:-1 group-title=\"{ch.group}\" tvg-logo=\"{ch.logo_url}\" tvg-id=\"{ch.tvg_id}\",{ch.name}");
                        writer.WriteLine(url);
                    }
                }
                SavedFilePath = dialog.FileName;
                IsSaved = true;
                MessageBox.Show($"Playlist saved to {dialog.FileName}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}