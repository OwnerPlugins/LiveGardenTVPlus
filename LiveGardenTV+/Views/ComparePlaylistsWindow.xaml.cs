using LiveGardenTVPlus.Models;
using LiveGardenTVPlus.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace LiveGardenTVPlus.Views
{
    public partial class ComparePlaylistsWindow : Window
    {
        private List<ChannelJson> _firstPlaylist;
        private List<ChannelJson> _secondPlaylist;
        private ObservableCollection<CompareResultItem> _results = new ObservableCollection<CompareResultItem>();

        public ComparePlaylistsWindow(List<ChannelJson> firstPlaylist)
        {
            InitializeComponent();
            _firstPlaylist = firstPlaylist ?? new List<ChannelJson>();
            StatusText.Text = $"First playlist: {_firstPlaylist.Count} channels loaded.";
            ResultsGrid.ItemsSource = _results;
            LanguageManager.LanguageChanged += ApplyLanguage;
            ApplyLanguage();
        }

        private void ApplyLanguage()
        {
            Title = LanguageManager.GetTranslation("Compare Playlists");
            LoadSecondFileBtn.Content = LanguageManager.GetTranslation("Load from file...");
            LoadSecondUrlBtn.Content = LanguageManager.GetTranslation("Load from URL...");
            CompareBtn.Content = LanguageManager.GetTranslation("Compare");
            ExportMissingBtn.Content = LanguageManager.GetTranslation("Export missing");
            ExportAllBtn.Content = LanguageManager.GetTranslation("Export all");
            CloseBtn.Content = LanguageManager.GetTranslation("Close");

            if (ResultsGrid.Columns.Count >= 5)
            {
                ResultsGrid.Columns[0].Header = LanguageManager.GetTranslation("Channel Name");
                ResultsGrid.Columns[1].Header = LanguageManager.GetTranslation("URL");
                ResultsGrid.Columns[2].Header = LanguageManager.GetTranslation("Status");
                ResultsGrid.Columns[3].Header = LanguageManager.GetTranslation("Group");
                ResultsGrid.Columns[4].Header = LanguageManager.GetTranslation("TvgId");
            }
        }

        private async void LoadSecondFileBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files|*.json|M3U files|*.m3u;*.m3u8|All files|*.*",
                DefaultExt = ".json"
            };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string ext = Path.GetExtension(dlg.FileName).ToLower();
                    if (ext == ".json")
                    {
                        var channels = JsonImportService.ImportFromFileWithMapping(dlg.FileName, this);
                        if (channels != null && channels.Count > 0)
                            _secondPlaylist = channels;
                        else
                            MessageBox.Show("No channels loaded.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else if (ext == ".m3u" || ext == ".m3u8")
                    {
                        var m3uChannels = M3uParser.Parse(dlg.FileName);
                        if (m3uChannels != null && m3uChannels.Count > 0)
                        {
                            _secondPlaylist = m3uChannels.Select(c => new ChannelJson
                            {
                                name = c.Name,
                                stream_urls = string.IsNullOrEmpty(c.Url) ? new List<string>() : new List<string> { c.Url },
                                logo_url = c.Logo,
                                group = c.Group,
                                tvg_id = c.TvgId,
                                isFavorite = c.IsFavorite
                            }).ToList();
                        }
                        else
                            MessageBox.Show("No channels loaded from M3U.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    }

                    if (_secondPlaylist != null && _secondPlaylist.Count > 0)
                    {
                        StatusText.Text = $"First: {_firstPlaylist.Count} | Second: {_secondPlaylist.Count} channels loaded. Click 'Compare'.";
                        CompareBtn.IsEnabled = true;
                        // Auto‑compare if we have both lists
                        if (_firstPlaylist.Count > 0 && _secondPlaylist.Count > 0)
                            PerformComparison();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void LoadSecondUrlBtn_Click(object sender, RoutedEventArgs e)
        {
            string url = Microsoft.VisualBasic.Interaction.InputBox(
                LanguageManager.GetTranslation("Enter playlist URL (JSON or M3U):"),
                LanguageManager.GetTranslation("Load from URL"),
                "");
            if (string.IsNullOrWhiteSpace(url)) return;

            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(60);
                client.DefaultRequestHeaders.Add("User-Agent", "LiveGardenTVPlus");
                string content = await client.GetStringAsync(url);

                bool isJson = (content.TrimStart().StartsWith("{") || content.TrimStart().StartsWith("[")) && !content.TrimStart().StartsWith("#EXTM3U");

                if (isJson)
                {
                    var channels = JsonImportService.ImportFromUrlWithMapping(content, "url_import.json", this);
                    if (channels != null && channels.Count > 0)
                        _secondPlaylist = channels;
                    else
                        MessageBox.Show("No channels loaded from JSON URL.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    string tempFile = Path.GetTempFileName();
                    await File.WriteAllTextAsync(tempFile, content);
                    var m3uChannels = M3uParser.Parse(tempFile);
                    File.Delete(tempFile);
                    if (m3uChannels != null && m3uChannels.Count > 0)
                    {
                        _secondPlaylist = m3uChannels.Select(c => new ChannelJson
                        {
                            name = c.Name,
                            stream_urls = string.IsNullOrEmpty(c.Url) ? new List<string>() : new List<string> { c.Url },
                            logo_url = c.Logo,
                            group = c.Group,
                            tvg_id = c.TvgId,
                            isFavorite = c.IsFavorite
                        }).ToList();
                    }
                    else
                        MessageBox.Show("No channels loaded from M3U URL.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                if (_secondPlaylist != null && _secondPlaylist.Count > 0)
                {
                    StatusText.Text = $"First: {_firstPlaylist.Count} | Second: {_secondPlaylist.Count} channels loaded. Click 'Compare'.";
                    CompareBtn.IsEnabled = true;
                    if (_firstPlaylist.Count > 0 && _secondPlaylist.Count > 0)
                        PerformComparison();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading URL: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CompareFieldCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_secondPlaylist != null && _secondPlaylist.Count > 0 && _firstPlaylist.Count > 0)
                PerformComparison();
        }

        private void CompareBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_secondPlaylist == null || _secondPlaylist.Count == 0)
            {
                MessageBox.Show("Please load a second playlist first.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            PerformComparison();
        }

        private void PerformComparison()
        {
            var selectedItem = CompareFieldCombo.SelectedItem as ComboBoxItem;
            string tag = selectedItem?.Tag?.ToString() ?? "Priority";
            CompareResult result;

            if (tag == "Priority")
            {
                result = PlaylistComparer.CompareWithPriority(_firstPlaylist, _secondPlaylist);
            }
            else
            {
                var field = (CompareField)Enum.Parse(typeof(CompareField), tag);
                result = PlaylistComparer.Compare(_firstPlaylist, _secondPlaylist, field);
            }

            _results.Clear();
            foreach (var ch in result.OnlyInFirst)
                _results.Add(new CompareResultItem { Name = ch.name, PrimaryUrl = ch.stream_urls?.FirstOrDefault() ?? "", Group = ch.group, TvgId = ch.tvg_id, Status = "Only in First", Source = ch });
            foreach (var ch in result.OnlyInSecond)
                _results.Add(new CompareResultItem { Name = ch.name, PrimaryUrl = ch.stream_urls?.FirstOrDefault() ?? "", Group = ch.group, TvgId = ch.tvg_id, Status = "Only in Second", Source = ch });
            foreach (var ch in result.InBoth)
                _results.Add(new CompareResultItem { Name = ch.name, PrimaryUrl = ch.stream_urls?.FirstOrDefault() ?? "", Group = ch.group, TvgId = ch.tvg_id, Status = "In Both", Source = ch });

            ResultsGrid.ItemsSource = null;
            ResultsGrid.ItemsSource = _results;

            StatusText.Text = $"Compared. Results: {_results.Count} channels.";
            ExportMissingBtn.IsEnabled = _results.Any(r => r.Status == "Only in Second");
            ExportAllBtn.IsEnabled = _secondPlaylist != null && _secondPlaylist.Count > 0;
            CompareBtn.IsEnabled = false;
        }

        private async void ExportMissingBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_secondPlaylist == null || _secondPlaylist.Count == 0)
            {
                MessageBox.Show("No second playlist loaded.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (_results.Count == 0)
            {
                MessageBox.Show("No comparison results. Run Compare first.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Get the full ChannelJson objects for missing channels (Only in Second)
            var missing = _results.Where(r => r.Status == "Only in Second")
                                  .Select(r => r.Source)
                                  .Where(ch => ch != null)
                                  .ToList();

            if (missing.Count == 0)
            {
                MessageBox.Show("No missing channels to export.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await ExportFullChannels(missing, "missing_channels");
        }

        private async void ExportAllBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_secondPlaylist == null || _secondPlaylist.Count == 0)
            {
                MessageBox.Show("No second playlist loaded.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await ExportFullChannels(_secondPlaylist, "all_channels");
        }

        private async Task ExportFullChannels(List<ChannelJson> channels, string defaultFileName)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON files|*.json|M3U files|*.m3u",
                DefaultExt = ".json",
                FileName = defaultFileName
            };

            if (dialog.ShowDialog() == true)
            {
                string ext = Path.GetExtension(dialog.FileName).ToLower();
                if (ext == ".json")
                {
                    // Export full JSON with all fields
                    string json = JsonConvert.SerializeObject(channels, Formatting.Indented);
                    await File.WriteAllTextAsync(dialog.FileName, json);
                }
                else if (ext == ".m3u")
                {
                    // Export M3U using first URL
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
                }
                MessageBox.Show($"Exported {channels.Count} channels to {dialog.FileName}", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ResultsGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;

            string sortProperty = e.Column.Header?.ToString() switch
            {
                "Channel Name" => "Name",
                "URL" => "PrimaryUrl",
                "Status" => "Status",
                "Group" => "Group",
                "TvgId" => "TvgId",
                _ => null
            };

            if (sortProperty == null) return;

            var view = CollectionViewSource.GetDefaultView(ResultsGrid.ItemsSource);
            if (view == null) return;

            ListSortDirection direction = ListSortDirection.Ascending;
            if (view.SortDescriptions.Count > 0 && view.SortDescriptions[0].PropertyName == sortProperty)
            {
                direction = view.SortDescriptions[0].Direction == ListSortDirection.Ascending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;
            }

            using (view.DeferRefresh())
            {
                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(new SortDescription(sortProperty, direction));
            }

            foreach (var col in ResultsGrid.Columns)
                col.SortDirection = null;
            e.Column.SortDirection = direction;
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

        public class CompareResultItem
        {
            public string Name { get; set; }
            public string PrimaryUrl { get; set; }
            public string Group { get; set; }
            public string TvgId { get; set; }
            public string Status { get; set; }
            public ChannelJson Source { get; set; }   // original object for export
        }
    }
}