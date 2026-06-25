using LiveGardenTVPlus.Models;
using LiveGardenTVPlus.Services;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
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

            if (CompareFieldCombo.SelectedIndex == -1 && CompareFieldCombo.Items.Count > 0)
                CompareFieldCombo.SelectedIndex = 0;

            // StatusText.Text = string.Format(LanguageManager.GetTranslation("First playlist: {0} channels loaded."), _firstPlaylist.Count);
            // StatusText.Text = string.Format(LanguageManager.GetTranslation("Comparison results: {0} channels"), _results.Count);
            StatusText.Text = string.Format(LanguageManager.GetTranslation("Comparison results: {0} channels"), _results.Count);
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

            foreach (ComboBoxItem item in CompareFieldCombo.Items)
            {
                string currentText = item.Content?.ToString() ?? "";
                if (!string.IsNullOrEmpty(currentText))
                {
                    string translation = LanguageManager.GetTranslation(currentText);
                    if (!string.IsNullOrEmpty(translation))
                        item.Content = translation;
                }
            }

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
                Filter = LanguageManager.GetTranslation("JSON files|*.json|M3U files|*.m3u;*.m3u8|All files|*.*"),
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
                            MessageBox.Show(LanguageManager.GetTranslation("No channels loaded."),
                                            LanguageManager.GetTranslation("Info"),
                                            MessageBoxButton.OK, MessageBoxImage.Information);
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
                            MessageBox.Show(LanguageManager.GetTranslation("No channels loaded from M3U."),
                                            LanguageManager.GetTranslation("Info"),
                                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }

                    if (_secondPlaylist != null && _secondPlaylist.Count > 0)
                    {
                        StatusText.Text = string.Format(LanguageManager.GetTranslation("Playlists loaded"), _firstPlaylist.Count, _secondPlaylist.Count);
                        CompareBtn.IsEnabled = true;
                        if (_firstPlaylist.Count > 0 && _secondPlaylist.Count > 0)
                            PerformComparison();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format(LanguageManager.GetTranslation("Error loading file"), ex.Message),
                                    LanguageManager.GetTranslation("Error"),
                                    MessageBoxButton.OK, MessageBoxImage.Error);
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
                        MessageBox.Show(LanguageManager.GetTranslation("No channels loaded from JSON URL."),
                                        LanguageManager.GetTranslation("Info"),
                                        MessageBoxButton.OK, MessageBoxImage.Information);
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
                        MessageBox.Show(LanguageManager.GetTranslation("No channels loaded from M3U URL."),
                                        LanguageManager.GetTranslation("Info"),
                                        MessageBoxButton.OK, MessageBoxImage.Information);
                }

                if (_secondPlaylist != null && _secondPlaylist.Count > 0)
                {
                    StatusText.Text = string.Format(LanguageManager.GetTranslation("Playlists loaded"), _firstPlaylist.Count, _secondPlaylist.Count);
                    CompareBtn.IsEnabled = true;
                    if (_firstPlaylist.Count > 0 && _secondPlaylist.Count > 0)
                        PerformComparison();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LanguageManager.GetTranslation("Error loading URL"), ex.Message),
                                LanguageManager.GetTranslation("Error"),
                                MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show(LanguageManager.GetTranslation("Load a second playlist first."),
                                LanguageManager.GetTranslation("Info"),
                                MessageBoxButton.OK, MessageBoxImage.Information);
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
                _results.Add(new CompareResultItem { Name = ch.name, PrimaryUrl = ch.stream_urls?.FirstOrDefault() ?? "", Group = ch.group, TvgId = ch.tvg_id, Status = LanguageManager.GetTranslation("Only in First"), Source = ch });
            foreach (var ch in result.OnlyInSecond)
                _results.Add(new CompareResultItem { Name = ch.name, PrimaryUrl = ch.stream_urls?.FirstOrDefault() ?? "", Group = ch.group, TvgId = ch.tvg_id, Status = LanguageManager.GetTranslation("Only in Second"), Source = ch });
            foreach (var ch in result.InBoth)
                _results.Add(new CompareResultItem { Name = ch.name, PrimaryUrl = ch.stream_urls?.FirstOrDefault() ?? "", Group = ch.group, TvgId = ch.tvg_id, Status = LanguageManager.GetTranslation("In Both"), Source = ch });

            ResultsGrid.ItemsSource = null;
            ResultsGrid.ItemsSource = _results;

            StatusText.Text = string.Format(LanguageManager.GetTranslation("Comparison results: {0} channels"), _results.Count);
            ExportMissingBtn.IsEnabled = _results.Any(r => r.Status == LanguageManager.GetTranslation("Only in Second"));
            ExportAllBtn.IsEnabled = _secondPlaylist != null && _secondPlaylist.Count > 0;
            CompareBtn.IsEnabled = false;
        }

        private async void ExportMissingBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_secondPlaylist == null || _secondPlaylist.Count == 0)
            {
                MessageBox.Show(LanguageManager.GetTranslation("No second playlist loaded."),
                                LanguageManager.GetTranslation("Info"),
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (_results.Count == 0)
            {
                MessageBox.Show(LanguageManager.GetTranslation("No comparison results. Run Compare first."),
                                LanguageManager.GetTranslation("Info"),
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var missing = _results.Where(r => r.Status == LanguageManager.GetTranslation("Only in Second"))
                                  .Select(r => r.Source)
                                  .Where(ch => ch != null)
                                  .ToList();

            if (missing.Count == 0)
            {
                MessageBox.Show(LanguageManager.GetTranslation("No missing channels to export."),
                                LanguageManager.GetTranslation("Info"),
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await ExportFullChannels(missing, "missing_channels");
        }

        private async void ExportAllBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_secondPlaylist == null || _secondPlaylist.Count == 0)
            {
                MessageBox.Show(LanguageManager.GetTranslation("No second playlist loaded."),
                                LanguageManager.GetTranslation("Info"),
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await ExportFullChannels(_secondPlaylist, "all_channels");
        }

        private async Task ExportFullChannels(List<ChannelJson> channels, string defaultFileName)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = LanguageManager.GetTranslation("JSON files|*.json|M3U files|*.m3u"),
                DefaultExt = ".json",
                FileName = defaultFileName
            };

            if (dialog.ShowDialog() == true)
            {
                string ext = Path.GetExtension(dialog.FileName).ToLower();
                if (ext == ".json")
                {
                    string json = JsonConvert.SerializeObject(channels, Formatting.Indented);
                    await File.WriteAllTextAsync(dialog.FileName, json);
                }
                else if (ext == ".m3u")
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
                }
                MessageBox.Show(string.Format(LanguageManager.GetTranslation("Exported channels"), channels.Count, dialog.FileName),
                                LanguageManager.GetTranslation("Export"),
                                MessageBoxButton.OK, MessageBoxImage.Information);
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
            public ChannelJson Source { get; set; }
        }
    }
}