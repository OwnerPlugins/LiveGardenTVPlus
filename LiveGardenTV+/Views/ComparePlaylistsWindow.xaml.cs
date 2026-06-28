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
        private ObservableCollection<CompareResultItem> _allResults = new ObservableCollection<CompareResultItem>();
        private ObservableCollection<CompareResultItem> _filteredResults = new ObservableCollection<CompareResultItem>();

        public ComparePlaylistsWindow(List<ChannelJson> firstPlaylist)
        {
            InitializeComponent();
            _firstPlaylist = firstPlaylist ?? new List<ChannelJson>();

            if (CompareFieldCombo.SelectedIndex == -1 && CompareFieldCombo.Items.Count > 0)
                CompareFieldCombo.SelectedIndex = 0;

            ResultsGrid.ItemsSource = _filteredResults;
            LanguageManager.LanguageChanged += ApplyLanguage;
            ApplyLanguage();

            ResultsGrid.SelectionChanged += (s, e) =>
            {
                ExportSelectedBtn.IsEnabled = ResultsGrid.SelectedItems.Count > 0;
            };
        }

        private void ApplyLanguage()
        {
            Title = LanguageManager.GetTranslation("Compare Playlists");
            LoadSecondFileBtn.Content = LanguageManager.GetTranslation("Load from file...");
            LoadSecondUrlBtn.Content = LanguageManager.GetTranslation("Load from URL...");
            CompareBtn.Content = LanguageManager.GetTranslation("Compare");
            ExportMissingBtn.Content = LanguageManager.GetTranslation("Export missing");
            ExportAllBtn.Content = LanguageManager.GetTranslation("Export all");
            ExportSelectedBtn.Content = LanguageManager.GetTranslation("Export selected");
            CloseBtn.Content = LanguageManager.GetTranslation("Close");
            FilterLabel.Text = LanguageManager.GetTranslation("Filters");
            ChannelInfoLabel.Text = LanguageManager.GetTranslation("Channel Info");
            EpgFavLabel.Text = LanguageManager.GetTranslation("EPG & Favorites");
            GeoCountryLabel.Text = LanguageManager.GetTranslation("Geo & Country");
            AdvancedIdsLabel.Text = LanguageManager.GetTranslation("Advanced IDs");
            UrlsStatusLabel.Text = LanguageManager.GetTranslation("URLs & Status");
            ApplyFilterBtn.Content = LanguageManager.GetTranslation("Apply");
            ClearFilterBtn.Content = LanguageManager.GetTranslation("Clear");

            NameFieldLabel.Text = LanguageManager.GetTranslation("Name");
            UrlFieldLabel.Text = LanguageManager.GetTranslation("URL");
            GroupFieldLabel.Text = LanguageManager.GetTranslation("Group");
            LogoFieldLabel.Text = LanguageManager.GetTranslation("Logo URL");
            TvgIdFieldLabel.Text = LanguageManager.GetTranslation("TvgId");
            CountryFieldLabel.Text = LanguageManager.GetTranslation("Country");
            NanoidFieldLabel.Text = LanguageManager.GetTranslation("Nanoid");
            LanguagesFieldLabel.Text = LanguageManager.GetTranslation("Languages (comma)");
            YoutubeFieldLabel.Text = LanguageManager.GetTranslation("Youtube URL");
            StreamFieldLabel.Text = LanguageManager.GetTranslation("Stream URL");
            StatusFieldLabel.Text = LanguageManager.GetTranslation("Status");
            FilterFavorite.Content = LanguageManager.GetTranslation("Favorite");
            FilterGeoBlocked.Content = LanguageManager.GetTranslation("GeoBlocked");

            if (ResultsGrid.Columns.Count >= 5)
            {
                ResultsGrid.Columns[0].Header = LanguageManager.GetTranslation("Channel Name");
                ResultsGrid.Columns[1].Header = LanguageManager.GetTranslation("URL");
                ResultsGrid.Columns[2].Header = LanguageManager.GetTranslation("Status");
                ResultsGrid.Columns[3].Header = LanguageManager.GetTranslation("Group");
                ResultsGrid.Columns[4].Header = LanguageManager.GetTranslation("TvgId");
            }

            foreach (ComboBoxItem item in CompareFieldCombo.Items)
            {
                string key = item.Tag?.ToString();
                if (!string.IsNullOrEmpty(key))
                {
                    string translation = LanguageManager.GetTranslation(key);
                    if (!string.IsNullOrEmpty(translation))
                        item.Content = translation;
                }
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
                            MessageBox.Show(LanguageManager.GetTranslation("No channels loaded."), "Info", MessageBoxButton.OK, MessageBoxImage.Information);
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
                            MessageBox.Show(LanguageManager.GetTranslation("No channels loaded from M3U."), "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    }

                    if (_secondPlaylist != null && _secondPlaylist.Count > 0)
                    {
                        StatusText.Text = string.Format(LanguageManager.GetTranslation("Second playlist loaded: {0} channels"), _secondPlaylist.Count);
                        CompareBtn.IsEnabled = true;
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
            string url = InputBoxHelper.ShowInputBox(
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
                        MessageBox.Show(LanguageManager.GetTranslation("No channels loaded from JSON URL."), "Info", MessageBoxButton.OK, MessageBoxImage.Information);
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
                        MessageBox.Show(LanguageManager.GetTranslation("No channels loaded from M3U URL."), "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                if (_secondPlaylist != null && _secondPlaylist.Count > 0)
                {
                    StatusText.Text = string.Format(LanguageManager.GetTranslation("Second playlist loaded: {0} channels"), _secondPlaylist.Count);
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
                MessageBox.Show(LanguageManager.GetTranslation("Load a second playlist first."), "Info", MessageBoxButton.OK, MessageBoxImage.Information);
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
                result = PlaylistComparer.CompareWithPriority(_firstPlaylist, _secondPlaylist);
            else
            {
                var field = (CompareField)Enum.Parse(typeof(CompareField), tag);
                result = PlaylistComparer.Compare(_firstPlaylist, _secondPlaylist, field);
            }

            _allResults.Clear();
            foreach (var ch in result.OnlyInFirst)
                _allResults.Add(new CompareResultItem { Name = ch.name, PrimaryUrl = ch.stream_urls?.FirstOrDefault() ?? "", Group = ch.group, TvgId = ch.tvg_id, Status = LanguageManager.GetTranslation("Only in First"), Source = ch });
            foreach (var ch in result.OnlyInSecond)
                _allResults.Add(new CompareResultItem { Name = ch.name, PrimaryUrl = ch.stream_urls?.FirstOrDefault() ?? "", Group = ch.group, TvgId = ch.tvg_id, Status = LanguageManager.GetTranslation("Only in Second"), Source = ch });
            foreach (var ch in result.InBoth)
                _allResults.Add(new CompareResultItem { Name = ch.name, PrimaryUrl = ch.stream_urls?.FirstOrDefault() ?? "", Group = ch.group, TvgId = ch.tvg_id, Status = LanguageManager.GetTranslation("In Both"), Source = ch });

            ApplyFilters();
            StatusText.Text = string.Format(LanguageManager.GetTranslation("Comparison results: {0} channels"), _filteredResults.Count);
            ExportMissingBtn.IsEnabled = _allResults.Any(r => r.Status == LanguageManager.GetTranslation("Only in Second"));
            ExportAllBtn.IsEnabled = _secondPlaylist != null && _secondPlaylist.Count > 0;
            CompareBtn.IsEnabled = false;
        }

        private void ApplyFilters()
        {
            var filtered = _allResults.AsEnumerable();

            if (!string.IsNullOrEmpty(FilterName.Text))
                filtered = filtered.Where(r => r.Name?.IndexOf(FilterName.Text, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!string.IsNullOrEmpty(FilterUrl.Text))
                filtered = filtered.Where(r => r.PrimaryUrl?.IndexOf(FilterUrl.Text, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!string.IsNullOrEmpty(FilterGroup.Text))
                filtered = filtered.Where(r => r.Group?.IndexOf(FilterGroup.Text, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!string.IsNullOrEmpty(FilterLogo.Text))
                filtered = filtered.Where(r => r.Source?.logo_url?.IndexOf(FilterLogo.Text, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!string.IsNullOrEmpty(FilterTvgId.Text))
                filtered = filtered.Where(r => r.TvgId?.IndexOf(FilterTvgId.Text, StringComparison.OrdinalIgnoreCase) >= 0);
            if (FilterFavorite.IsChecked == true)
                filtered = filtered.Where(r => r.Source?.isFavorite == true);
            if (!string.IsNullOrEmpty(FilterCountry.Text))
                filtered = filtered.Where(r => r.Source?.country?.IndexOf(FilterCountry.Text, StringComparison.OrdinalIgnoreCase) >= 0);
            if (FilterGeoBlocked.IsChecked == true)
                filtered = filtered.Where(r => r.Source?.isGeoBlocked == true);
            if (!string.IsNullOrEmpty(FilterNanoid.Text))
                filtered = filtered.Where(r => r.Source?.nanoid?.IndexOf(FilterNanoid.Text, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!string.IsNullOrEmpty(FilterLanguages.Text))
            {
                var langList = FilterLanguages.Text.Split(',').Select(l => l.Trim().ToLower());
                filtered = filtered.Where(r => r.Source?.languages != null && r.Source.languages.Any(l => langList.Contains(l.ToLower())));
            }
            if (!string.IsNullOrEmpty(FilterYoutube.Text))
                filtered = filtered.Where(r => r.Source?.youtube_urls != null && r.Source.youtube_urls.Any(u => u.IndexOf(FilterYoutube.Text, StringComparison.OrdinalIgnoreCase) >= 0));
            if (!string.IsNullOrEmpty(FilterStream.Text))
                filtered = filtered.Where(r => r.Source?.stream_urls != null && r.Source.stream_urls.Any(u => u.IndexOf(FilterStream.Text, StringComparison.OrdinalIgnoreCase) >= 0));
            if (!string.IsNullOrEmpty(FilterStatus.Text))
                filtered = filtered.Where(r => r.Status?.IndexOf(FilterStatus.Text, StringComparison.OrdinalIgnoreCase) >= 0);

            _filteredResults.Clear();
            foreach (var item in filtered)
                _filteredResults.Add(item);

            ResultsGrid.ItemsSource = null;
            ResultsGrid.ItemsSource = _filteredResults;
            StatusText.Text = string.Format(LanguageManager.GetTranslation("Comparison results: {0} channels"), _filteredResults.Count);
        }

        private void ApplyFilterBtn_Click(object sender, RoutedEventArgs e) => ApplyFilters();
        private void ClearFilterBtn_Click(object sender, RoutedEventArgs e)
        {
            FilterName.Text = FilterUrl.Text = FilterGroup.Text = FilterLogo.Text = FilterTvgId.Text =
            FilterCountry.Text = FilterNanoid.Text = FilterLanguages.Text = FilterYoutube.Text =
            FilterStream.Text = FilterStatus.Text = "";
            FilterFavorite.IsChecked = FilterGeoBlocked.IsChecked = null;
            ApplyFilters();
        }

        private async void ExportSelectedBtn_Click(object sender, RoutedEventArgs e)
        {
            var selected = ResultsGrid.SelectedItems.Cast<CompareResultItem>()
                .Select(r => r.Source)
                .Where(ch => ch != null)
                .ToList();

            if (selected.Count == 0)
            {
                MessageBox.Show(LanguageManager.GetTranslation("No channels selected."), "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await ExportFullChannels(selected, "selected_channels");
        }

        private async void ExportMissingBtn_Click(object sender, RoutedEventArgs e)
        {
            var missing = _allResults.Where(r => r.Status == LanguageManager.GetTranslation("Only in Second"))
                                     .Select(r => r.Source)
                                     .Where(ch => ch != null)
                                     .ToList();

            if (missing.Count == 0)
            {
                MessageBox.Show(LanguageManager.GetTranslation("No missing channels to export."), "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await ExportFullChannels(missing, "missing_channels");
        }

        private async void ExportAllBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_secondPlaylist == null || _secondPlaylist.Count == 0)
            {
                MessageBox.Show(LanguageManager.GetTranslation("No second playlist loaded."), "Info", MessageBoxButton.OK, MessageBoxImage.Information);
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
                MessageBox.Show(string.Format(LanguageManager.GetTranslation("Exported {0} channels to {1}"), channels.Count, dialog.FileName), "Export", MessageBoxButton.OK, MessageBoxImage.Information);
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
                direction = view.SortDescriptions[0].Direction == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;

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