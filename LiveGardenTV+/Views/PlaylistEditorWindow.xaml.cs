using LiveGardenTVPlus.Models;
using LiveGardenTVPlus.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
        private CancellationTokenSource _cancellationTokenSource = null;

        // Code tab fields
        private bool _isCodeTabUpdating = false;
        private bool _isParsingCode = false;
        private string _currentDisplayFormat = "Json";

        public PlaylistEditorWindow(List<Channel> channels, EpgService epgService = null)
        {
            InitializeComponent();
            DataContext = this;
            _epgService = epgService;

            // Convert Channel list to ChannelJson
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

            MainTabControl.SelectionChanged += MainTabControl_SelectionChanged;
        }

        private void ApplyLanguage()
        {
            // Group headers
            var playlistGroups = FindName("PlaylistGroups") as TextBlock;
            if (playlistGroups != null) playlistGroups.Text = LanguageManager.GetTranslation("Playlist & Groups");

            var epgLogos = FindName("EpgLogos") as TextBlock;
            if (epgLogos != null) epgLogos.Text = LanguageManager.GetTranslation("Epg & Logos");

            var openPlaylist = FindName("OpenPlaylist") as TextBlock;
            if (openPlaylist != null) openPlaylist.Text = LanguageManager.GetTranslation("Open Playlist");

            var jsonGroup = FindName("JsonGroup") as TextBlock;
            if (jsonGroup != null) jsonGroup.Text = LanguageManager.GetTranslation("Json");

            var urlCheck = FindName("URLCheck") as TextBlock;
            if (urlCheck != null) urlCheck.Text = LanguageManager.GetTranslation("URL Check");

            var actions = FindName("ActionsButtons") as TextBlock;
            if (actions != null) actions.Text = LanguageManager.GetTranslation("Actions");

            // Filter section headers
            var filterLabel = FindName("FilterLabel") as TextBlock;
            if (filterLabel != null) filterLabel.Text = LanguageManager.GetTranslation("Filters");

            var channelInfo = FindName("ChannelInfoLabel") as TextBlock;
            if (channelInfo != null) channelInfo.Text = LanguageManager.GetTranslation("Channel Info");

            var epgFav = FindName("EpgFavLabel") as TextBlock;
            if (epgFav != null) epgFav.Text = LanguageManager.GetTranslation("Epg & Favorites");

            var geoCountry = FindName("GeoCountryLabel") as TextBlock;
            if (geoCountry != null) geoCountry.Text = LanguageManager.GetTranslation("Geo & Country");

            var advancedIds = FindName("AdvancedIdsLabel") as TextBlock;
            if (advancedIds != null) advancedIds.Text = LanguageManager.GetTranslation("Advanced IDs");

            var urlsStatus = FindName("UrlsStatusLabel") as TextBlock;
            if (urlsStatus != null) urlsStatus.Text = LanguageManager.GetTranslation("URLs & Status");

            // Field labels
            var nameField = FindName("NameFieldLabel") as TextBlock;
            if (nameField != null) nameField.Text = LanguageManager.GetTranslation("Name");

            var urlField = FindName("UrlFieldLabel") as TextBlock;
            if (urlField != null) urlField.Text = LanguageManager.GetTranslation("URL");

            var groupField = FindName("GroupFieldLabel") as TextBlock;
            if (groupField != null) groupField.Text = LanguageManager.GetTranslation("Group");

            var logoField = FindName("LogoFieldLabel") as TextBlock;
            if (logoField != null) logoField.Text = LanguageManager.GetTranslation("Logo URL");

            var tvgIdField = FindName("TvgIdFieldLabel") as TextBlock;
            if (tvgIdField != null) tvgIdField.Text = LanguageManager.GetTranslation("TvgId");

            var countryField = FindName("CountryFieldLabel") as TextBlock;
            if (countryField != null) countryField.Text = LanguageManager.GetTranslation("Country");

            var nanoidField = FindName("NanoidFieldLabel") as TextBlock;
            if (nanoidField != null) nanoidField.Text = LanguageManager.GetTranslation("Nanoid");

            var languagesField = FindName("LanguagesFieldLabel") as TextBlock;
            if (languagesField != null) languagesField.Text = LanguageManager.GetTranslation("Languages (comma)");

            var youtubeField = FindName("YoutubeFieldLabel") as TextBlock;
            if (youtubeField != null) youtubeField.Text = LanguageManager.GetTranslation("Youtube URL");

            var streamField = FindName("StreamFieldLabel") as TextBlock;
            if (streamField != null) streamField.Text = LanguageManager.GetTranslation("Stream URL");

            var statusField = FindName("StatusFieldLabel") as TextBlock;
            if (statusField != null) statusField.Text = LanguageManager.GetTranslation("Status");

            var displayLabel = FindName("DisplayLabel") as TextBlock;
            if (displayLabel != null) displayLabel.Text = LanguageManager.GetTranslation("Display as");


            // Buttons
            NewPlaylistBtn.Content = LanguageManager.GetTranslation("New Playlist");
            AddGroupBtn.Content = LanguageManager.GetTranslation("Add Group");
            RenameGroupBtn.Content = LanguageManager.GetTranslation("Rename Group");
            DeleteGroupBtn.Content = LanguageManager.GetTranslation("Delete Group");

            OpenFileBtn.Content = LanguageManager.GetTranslation("Open File...");
            OpenUrlBtn.Content = LanguageManager.GetTranslation("Open from URL");

            CheckUrlsBtn.Content = LanguageManager.GetTranslation("Check URLs");
            CheckDuplicatesBtn.Content = LanguageManager.GetTranslation("Check Duplicates");
            SaveStatusBtn.Content = LanguageManager.GetTranslation("Save Status");

            ExportOkBtn.Content = LanguageManager.GetTranslation("Export OK");
            ExportFailedBtn.Content = LanguageManager.GetTranslation("Export KO");
            ExportFilteredM3uBtn.Content = LanguageManager.GetTranslation("Export Filtered M3U");
            ExportFilteredJsonBtn.Content = LanguageManager.GetTranslation("Export Filtered JSON");
            EnrichWithEpgBtn.Content = LanguageManager.GetTranslation("Enrich with EPG");

            FetchLogosBtn.Content = LanguageManager.GetTranslation("Fetch Logos");
            ResetOrderBtn.Content = LanguageManager.GetTranslation("Reset Order");

            ImportJsonBtn.Content = LanguageManager.GetTranslation("Import Local");
            ImportJsonUrlBtn.Content = LanguageManager.GetTranslation("Import from URL");
            ExportJsonBtn.Content = LanguageManager.GetTranslation("Export JSON");
            CompareBtn.Content = LanguageManager.GetTranslation("Compare...");

            DeleteSelectedBtn.Content = LanguageManager.GetTranslation("Delete Selected");
            ExportSelectedBtn.Content = LanguageManager.GetTranslation("Export Selected");
            PlayBtn.Content = LanguageManager.GetTranslation("▶ Play Media");
            StopBtn.Content = LanguageManager.GetTranslation("Stop");

            SaveBtn.Content = LanguageManager.GetTranslation("Save as...");
            CloseBtn.Content = LanguageManager.GetTranslation("Exit");

            ApplyFilterBtn.Content = LanguageManager.GetTranslation("Apply Filters");
            ClearFilterBtn.Content = LanguageManager.GetTranslation("Clear Filters");

            // Tab Control
            GridTab.Header = LanguageManager.GetTranslation("Grid");
            CodeTab.Header = LanguageManager.GetTranslation("Code");

            // Code Tab buttons
            RefreshCodeBtn.Content = LanguageManager.GetTranslation("Refresh Code");
            ApplyCodeBtn.Content = LanguageManager.GetTranslation("Apply / Import");
            CopyCodeBtn.Content = LanguageManager.GetTranslation("Copy");

            // DataGrid columns
            colPicons.Header = LanguageManager.GetTranslation("Image");
            colName.Header = LanguageManager.GetTranslation("Name");
            colUrlPrimary.Header = LanguageManager.GetTranslation("URL (primary)");
            colGroup.Header = LanguageManager.GetTranslation("Group");
            colLogo.Header = LanguageManager.GetTranslation("Logo");
            colTvgId.Header = LanguageManager.GetTranslation("TvgId");
            colFavorite.Header = LanguageManager.GetTranslation("Favorite");
            colCountry.Header = LanguageManager.GetTranslation("Country");
            colGeoBlocked.Header = LanguageManager.GetTranslation("GeoBlocked");
            colNanoid.Header = LanguageManager.GetTranslation("Nanoid");
            colLanguages.Header = LanguageManager.GetTranslation("Languages");
            colYoutube.Header = LanguageManager.GetTranslation("Youtube URLs");
            colStreamUrls.Header = LanguageManager.GetTranslation("Stream URLs");
            colStatus.Header = LanguageManager.GetTranslation("Status");

            FilterFavorite.Content = LanguageManager.GetTranslation("Favorite");
            FilterGeoBlocked.Content = LanguageManager.GetTranslation("GeoBlocked");

            Title = LanguageManager.GetTranslation("Playlist Management");
        }

        private void CodeTab_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (ComboBoxItem item in DisplayFormatCombo.Items)
            {
                if (item.Tag?.ToString() == "Json")
                    item.Content = LanguageManager.GetTranslation("JSON");
                else if (item.Tag?.ToString() == "M3u")
                    item.Content = LanguageManager.GetTranslation("M3U");
            }

            foreach (ComboBoxItem item in FormatCombo.Items)
            {
                if (item.Tag?.ToString() == "Auto")
                    item.Content = LanguageManager.GetTranslation("Auto (recommended)");
                else if (item.Tag?.ToString() == "Json")
                    item.Content = LanguageManager.GetTranslation("JSON");
                else if (item.Tag?.ToString() == "M3u")
                    item.Content = LanguageManager.GetTranslation("M3U");
            }
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
                string newGroupName = InputBoxHelper.ShowInputBox(
                    LanguageManager.GetTranslation("Enter new group name:"),
                    LanguageManager.GetTranslation("Add Group"),
                    "");
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
                string newGroup = InputBoxHelper.ShowInputBox(
                    string.Format(LanguageManager.GetTranslation("Rename group '{0}' to:"), oldGroup),
                    LanguageManager.GetTranslation("Rename Group"),
                    oldGroup);
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
        // Code Tab methods
        // ------------------------------------------------------------------
        private async Task<string> GenerateCodeContentAsync(string format = null)
        {
            if (string.IsNullOrEmpty(format))
                format = _currentDisplayFormat;

            return await Task.Run(() =>
            {
                if (format == "M3u")
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("#EXTM3U");
                    foreach (var ch in Channels)
                    {
                        string url = ch.stream_urls?.FirstOrDefault() ?? "";
                        if (string.IsNullOrEmpty(url)) continue;
                        string logoAttr = string.IsNullOrEmpty(ch.logo_url) ? "" : $" tvg-logo=\"{ch.logo_url}\"";
                        string tvgIdAttr = string.IsNullOrEmpty(ch.tvg_id) ? "" : $" tvg-id=\"{ch.tvg_id}\"";
                        sb.AppendLine($"#EXTINF:-1 group-title=\"{ch.group}\"{logoAttr}{tvgIdAttr},{ch.name}");
                        sb.AppendLine(url);
                    }
                    return sb.ToString();
                }
                else
                {
                    try
                    {
                        return JsonConvert.SerializeObject(Channels, Formatting.Indented);
                    }
                    catch
                    {
                        return "[]";
                    }
                }
            });
        }

        private async void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainTabControl.SelectedItem == CodeTab)
            {
                // ---- TRANSLATE CODE TAB CONTROLS ----
                var displayLabel = FindName("DisplayLabel") as TextBlock;
                if (displayLabel != null) displayLabel.Text = LanguageManager.GetTranslation("Display as");

                foreach (ComboBoxItem item in DisplayFormatCombo.Items)
                {
                    if (item.Tag?.ToString() == "Json")
                        item.Content = LanguageManager.GetTranslation("JSON");
                    else if (item.Tag?.ToString() == "M3u")
                        item.Content = LanguageManager.GetTranslation("M3U");
                }

                foreach (ComboBoxItem item in FormatCombo.Items)
                {
                    if (item.Tag?.ToString() == "Auto")
                        item.Content = LanguageManager.GetTranslation("Auto (recommended)");
                    else if (item.Tag?.ToString() == "Json")
                        item.Content = LanguageManager.GetTranslation("JSON");
                    else if (item.Tag?.ToString() == "M3u")
                        item.Content = LanguageManager.GetTranslation("M3U");
                }

                // ---- END TRANSLATIONS ----

                if (!_isParsingCode && !_isCodeTabUpdating)
                {
                    _isCodeTabUpdating = true;
                    try
                    {
                        string content = await GenerateCodeContentAsync();
                        CodeTextBox.Text = content;
                    }
                    catch (Exception ex)
                    {
                        CodeTextBox.Text = $"// Error generating code: {ex.Message}";
                    }
                    finally
                    {
                        _isCodeTabUpdating = false;
                    }
                }
            }
        }

        private async void RefreshCodeBtn_Click(object sender, RoutedEventArgs e)
        {
            string content = await GenerateCodeContentAsync();
            CodeTextBox.Text = content;
        }

        private void CopyCodeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(CodeTextBox.Text))
            {
                Clipboard.SetText(CodeTextBox.Text);
                MessageBox.Show(LanguageManager.GetTranslation("Code copied to clipboard."),
                                LanguageManager.GetTranslation("Success"),
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void ApplyCodeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isParsingCode) return;
            _isParsingCode = true;
            try
            {
                string text = CodeTextBox.Text.Trim();
                if (string.IsNullOrEmpty(text))
                {
                    MessageBox.Show(LanguageManager.GetTranslation("No text to import."),
                                    LanguageManager.GetTranslation("Info"),
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string selectedFormat = "Auto";
                if (FormatCombo.SelectedItem is ComboBoxItem item && item.Tag != null)
                    selectedFormat = item.Tag.ToString();

                List<ChannelJson> importedChannels = null;
                string errorMessage = null;

                bool isJson = false;
                bool isM3u = false;

                if (selectedFormat == "Auto")
                {
                    string trimmed = text.TrimStart();
                    isJson = (trimmed.StartsWith("{") || trimmed.StartsWith("[")) && !trimmed.StartsWith("#EXTM3U");
                    isM3u = trimmed.StartsWith("#EXTM3U") || trimmed.Contains("#EXTM3U") || trimmed.Contains("#EXTINF");
                    if (!isJson && !isM3u)
                    {
                        MessageBox.Show(LanguageManager.GetTranslation("Unable to auto-detect format. Please select JSON or M3U from the dropdown and try again."),
                                        LanguageManager.GetTranslation("Format Detection"),
                                        MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                else if (selectedFormat == "Json")
                    isJson = true;
                else if (selectedFormat == "M3u")
                    isM3u = true;

                if (isJson)
                {
                    try
                    {
                        importedChannels = JsonConvert.DeserializeObject<List<ChannelJson>>(text);
                        if (importedChannels == null || importedChannels.Count == 0)
                            errorMessage = LanguageManager.GetTranslation("No channels found in JSON.");
                    }
                    catch (JsonReaderException jex)
                    {
                        errorMessage = string.Format(LanguageManager.GetTranslation("Invalid JSON: {0}"), jex.Message);
                    }
                    catch (Exception ex)
                    {
                        errorMessage = string.Format(LanguageManager.GetTranslation("JSON parsing error: {0}"), ex.Message);
                    }
                }
                else if (isM3u)
                {
                    try
                    {
                        string tempFile = Path.GetTempFileName();
                        await File.WriteAllTextAsync(tempFile, text);
                        var m3uChannels = M3uParser.Parse(tempFile);
                        File.Delete(tempFile);
                        if (m3uChannels == null || m3uChannels.Count == 0)
                            errorMessage = LanguageManager.GetTranslation("No channels found in M3U.");
                        else
                        {
                            importedChannels = m3uChannels.Select(c => new ChannelJson
                            {
                                name = c.Name,
                                stream_urls = string.IsNullOrEmpty(c.Url) ? new List<string>() : new List<string> { c.Url },
                                logo_url = c.Logo,
                                group = c.Group,
                                tvg_id = c.TvgId,
                                isFavorite = c.IsFavorite
                            }).ToList();
                        }
                    }
                    catch (Exception ex)
                    {
                        errorMessage = string.Format(LanguageManager.GetTranslation("M3U parsing error: {0}"), ex.Message);
                    }
                }
                else
                {
                    errorMessage = LanguageManager.GetTranslation("Unsupported format. Please select JSON or M3U.");
                }

                if (errorMessage != null)
                {
                    MessageBox.Show(errorMessage, LanguageManager.GetTranslation("Import Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (importedChannels == null || importedChannels.Count == 0)
                {
                    MessageBox.Show(LanguageManager.GetTranslation("No channels to import."),
                                    LanguageManager.GetTranslation("Info"),
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                Channels.Clear();
                foreach (var ch in importedChannels)
                {
                    ch.stream_urls ??= new List<string>();
                    ch.youtube_urls ??= new List<string>();
                    ch.languages ??= new List<string>();
                    Channels.Add(ch);
                }

                ClearFilterBtn_Click(null, null);
                UpdateFilteredCount();
                MessageBox.Show(string.Format(LanguageManager.GetTranslation("Imported {0} channels from code."), importedChannels.Count),
                                LanguageManager.GetTranslation("Success"), MessageBoxButton.OK, MessageBoxImage.Information);

                string newContent = await GenerateCodeContentAsync();
                CodeTextBox.Text = newContent;
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LanguageManager.GetTranslation("Import error: {0}"), ex.Message),
                                LanguageManager.GetTranslation("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isParsingCode = false;
            }
        }

        private async void DisplayFormatCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isParsingCode || _isCodeTabUpdating) return;

            var item = DisplayFormatCombo.SelectedItem as ComboBoxItem;
            if (item == null) return;

            _currentDisplayFormat = item.Tag?.ToString() ?? "Json";
            if (MainTabControl.SelectedItem == CodeTab)
            {
                string content = await GenerateCodeContentAsync();
                CodeTextBox.Text = content;
            }
        }

        // ------------------------------------------------------------------
        // URL check
        // ------------------------------------------------------------------
        private async void CheckUrlsBtn_Click(object sender, RoutedEventArgs e)
        {
            var visibleChannels = ChannelsGrid.ItemsSource as IEnumerable<ChannelJson>;
            if (visibleChannels == null || !visibleChannels.Any())
            {
                MessageBox.Show("No channels to check (filters are active but no results).", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            CheckUrlsBtn.IsEnabled = false;
            StopBtn.IsEnabled = true;
            CheckProgressBar.Visibility = Visibility.Visible;
            CheckProgressBar.IsIndeterminate = true;

            try
            {
                var progress = new Progress<KeyValuePair<ChannelJson, string>>(UpdateUrlStatus);
                await Task.Run(() => CheckAllUrls(visibleChannels.ToList(), progress, token), token);
                MessageBox.Show($"URL check completed on {visibleChannels.Count()} channels.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("URL check was cancelled.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            finally
            {
                CheckUrlsBtn.IsEnabled = true;
                StopBtn.IsEnabled = false;
                CheckProgressBar.Visibility = Visibility.Collapsed;
                CheckProgressBar.IsIndeterminate = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void UpdateUrlStatus(KeyValuePair<ChannelJson, string> result)
        {
            result.Key.UrlStatus = result.Value;
            Dispatcher.Invoke(() => ChannelsGrid.Items.Refresh());
        }

        private void CheckAllUrls(List<ChannelJson> channelsToCheck, IProgress<KeyValuePair<ChannelJson, string>> progress, CancellationToken token)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(5);
                foreach (var channel in channelsToCheck)
                {
                    token.ThrowIfCancellationRequested();
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
                        status = "KO";
                    progress.Report(new KeyValuePair<ChannelJson, string>(channel, status));
                }
            }
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            StopBtn.IsEnabled = false;
        }

        // ------------------------------------------------------------------
        // Export Selected
        // ------------------------------------------------------------------
        private async void ExportSelectedBtn_Click(object sender, RoutedEventArgs e)
        {
            var selected = ChannelsGrid.SelectedItems.Cast<ChannelJson>().ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show(LanguageManager.GetTranslation("No channels selected."), "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            ExportWithFormatChoice(selected, "selected_channels");
        }

        // ------------------------------------------------------------------
        // Delete Selected
        // ------------------------------------------------------------------
        private void DeleteSelectedBtn_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = ChannelsGrid.SelectedItems.Cast<ChannelJson>().ToList();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("No channels selected.", "Delete", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Are you sure you want to delete {selectedItems.Count} selected channel(s)?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            foreach (var ch in selectedItems)
                Channels.Remove(ch);

            UpdateFilteredCount();
            ApplyFilter();
            ChannelsGrid.Items.Refresh();
            MessageBox.Show($"{selectedItems.Count} channel(s) deleted.", "Delete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ------------------------------------------------------------------
        // Play button (opens MiniPlayer)
        // ------------------------------------------------------------------
        private void PlayBtn_Click(object sender, RoutedEventArgs e)
        {
            var selected = ChannelsGrid.SelectedItem as ChannelJson;
            if (selected == null)
            {
                MessageBox.Show(LanguageManager.GetTranslation("Select a channel to play."),
                                LanguageManager.GetTranslation("Info"),
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string url = selected.stream_urls?.FirstOrDefault() ?? "";
            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show(LanguageManager.GetTranslation("No stream URL available for this channel."),
                                LanguageManager.GetTranslation("Info"),
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int index = Channels.IndexOf(selected);
            var player = new MiniPlayerWindow(Channels.ToList(), index);
            player.Owner = this;
            player.ShowDialog();
        }

        // ------------------------------------------------------------------
        // Save Status
        // ------------------------------------------------------------------
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
        // Export helpers
        // ------------------------------------------------------------------
        private void ExportOkBtn_Click(object sender, RoutedEventArgs e)
        {
            var visibleChannels = ChannelsGrid.ItemsSource as IEnumerable<ChannelJson>;
            if (visibleChannels == null || !visibleChannels.Any())
            {
                MessageBox.Show("No channels to export.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var working = visibleChannels
                .Where(c => c.UrlStatus?.Contains("OK") == true && !c.UrlStatus.StartsWith("KO"))
                .ToList();
            if (working.Count == 0)
            {
                MessageBox.Show("No working channels to export.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            ExportWithFormatChoice(working, "working_channels");
        }

        private void ExportFailedBtn_Click(object sender, RoutedEventArgs e)
        {
            var visibleChannels = ChannelsGrid.ItemsSource as IEnumerable<ChannelJson>;
            if (visibleChannels == null || !visibleChannels.Any())
            {
                MessageBox.Show("No channels to export.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var failed = visibleChannels.Where(c => c.UrlStatus?.StartsWith("KO") == true).ToList();
            if (failed.Count == 0)
            {
                MessageBox.Show("No failed channels to export.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            ExportWithFormatChoice(failed, "failed_channels");
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

        private void ExportWithFormatChoice(List<ChannelJson> channels, string defaultFileName)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "M3U files|*.m3u|JSON files|*.json",
                DefaultExt = ".m3u",
                FileName = defaultFileName + ".m3u"
            };

            if (dialog.ShowDialog() == true)
            {
                string filePath = dialog.FileName;
                string extension = System.IO.Path.GetExtension(filePath).ToLower();

                if (extension == ".m3u")
                {
                    ExportChannelsToM3u(channels, filePath);
                }
                else if (extension == ".json")
                {
                    ExportToJson(filePath, channels);
                }
                else
                {
                    MessageBox.Show("Unsupported file format.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                MessageBox.Show($"Exported {channels.Count} channels to {filePath}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
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
        // JSON Import / Export
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
                var mappedChannels = JsonImportService.ImportFromFileWithMapping(dialog.FileName, this);
                if (mappedChannels != null && mappedChannels.Count > 0)
                {
                    Channels.Clear();
                    foreach (var ch in mappedChannels)
                        Channels.Add(ch);
                    ClearFilterBtn_Click(null, null);
                    MessageBox.Show(string.Format(LanguageManager.GetTranslation("Imported {0} channels from JSON via mapping."), mappedChannels.Count),
                                    LanguageManager.GetTranslation("Success"), MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(LanguageManager.GetTranslation("No channels could be mapped or import cancelled."),
                                    LanguageManager.GetTranslation("Info"), MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private async void ImportJsonUrlBtn_Click(object sender, RoutedEventArgs e)
        {
            string url = InputBoxHelper.ShowInputBox(
                LanguageManager.GetTranslation("Enter JSON URL:"),
                LanguageManager.GetTranslation("Import JSON from URL"),
                "");

            if (string.IsNullOrWhiteSpace(url)) return;

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "LiveGardenTVPlus");
                string jsonContent = await client.GetStringAsync(url);

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
        // Compare
        // ------------------------------------------------------------------
        private void CompareBtn_Click(object sender, RoutedEventArgs e)
        {
            var first = Channels.ToList();
            if (first.Count == 0)
            {
                MessageBox.Show("Current playlist is empty.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var window = new ComparePlaylistsWindow(first);
            window.Owner = this;
            window.ShowDialog();
        }

        // ------------------------------------------------------------------
        // Enrich with EPG
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
        // Fetch Logos
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
            this.Cursor = Cursors.Wait;
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
                this.Cursor = Cursors.Arrow;
                _isFetchingLogos = false;
            }
        }

        // ------------------------------------------------------------------
        // Open File / URL
        // ------------------------------------------------------------------
        private async void OpenFileBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Playlist files|*.m3u;*.m3u8;*.json|M3U files|*.m3u;*.m3u8|JSON files|*.json",
                DefaultExt = ".m3u"
            };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string filePath = dlg.FileName;
                    string ext = Path.GetExtension(filePath).ToLower();

                    if (ext == ".json")
                    {
                        var mappedChannels = JsonImportService.ImportFromFileWithMapping(filePath, this);
                        if (mappedChannels != null && mappedChannels.Count > 0)
                        {
                            Channels.Clear();
                            foreach (var ch in mappedChannels)
                                Channels.Add(ch);
                            ClearFilterBtn_Click(null, null);
                            MessageBox.Show(string.Format(LanguageManager.GetTranslation("Loaded {0} channels from JSON file via mapping."), mappedChannels.Count),
                                            LanguageManager.GetTranslation("Success"), MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        return;
                    }
                    else
                    {
                        var m3uChannels = M3uParser.Parse(filePath);
                        if (m3uChannels == null || m3uChannels.Count == 0)
                            throw new Exception("No channels found in M3U file.");

                        var channels = m3uChannels.Select(c => new ChannelJson
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

                        Channels.Clear();
                        foreach (var ch in channels)
                            Channels.Add(ch);
                        ClearFilterBtn_Click(null, null);
                        MessageBox.Show(string.Format(LanguageManager.GetTranslation("Loaded {0} channels from M3U file."), channels.Count),
                                        LanguageManager.GetTranslation("Success"), MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format(LanguageManager.GetTranslation("Error loading file: {0}"), ex.Message),
                                    LanguageManager.GetTranslation("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void OpenUrlBtn_Click(object sender, RoutedEventArgs e)
        {
            string url = InputBoxHelper.ShowInputBox(
                LanguageManager.GetTranslation("Enter playlist URL (M3U or JSON):"),
                LanguageManager.GetTranslation("Open from URL"),
                "");
            if (string.IsNullOrWhiteSpace(url)) return;

            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(60);
                client.DefaultRequestHeaders.Add("User-Agent", "LiveGardenTVPlus");
                string content = await client.GetStringAsync(url);

                string trimmed = content.TrimStart();
                bool isJson = (trimmed.StartsWith("{") || trimmed.StartsWith("[")) && !trimmed.StartsWith("#EXTM3U");

                if (isJson)
                {
                    var channels = ParseJsonToChannelJsonList(content);
                    if (channels != null && channels.Count > 0)
                    {
                        Channels.Clear();
                        foreach (var ch in channels)
                            Channels.Add(ch);
                        ClearFilterBtn_Click(null, null);
                        MessageBox.Show(string.Format(LanguageManager.GetTranslation("Loaded {0} channels from JSON URL."), channels.Count),
                                        LanguageManager.GetTranslation("Success"), MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                        throw new Exception("No channels found in JSON.");
                }
                else
                {
                    // M3U parsing with Task.Run
                    List<Channel> m3uChannels = null;
                    await Task.Run(() =>
                    {
                        string tempFile = Path.GetTempFileName();
                        File.WriteAllText(tempFile, content);
                        m3uChannels = M3uParser.Parse(tempFile);
                        File.Delete(tempFile);
                    });

                    if (m3uChannels == null || m3uChannels.Count == 0)
                        throw new Exception("No channels found in M3U.");

                    var channels = m3uChannels.Select(c => new ChannelJson
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

                    Channels.Clear();
                    foreach (var ch in channels)
                        Channels.Add(ch);
                    ClearFilterBtn_Click(null, null);
                    MessageBox.Show(string.Format(LanguageManager.GetTranslation("Loaded {0} channels from M3U URL."), channels.Count),
                                    LanguageManager.GetTranslation("Success"), MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LanguageManager.GetTranslation("Error loading URL: {0}"), ex.Message),
                                LanguageManager.GetTranslation("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ------------------------------------------------------------------
        // JSON parsing helper
        // ------------------------------------------------------------------
        private List<ChannelJson> ParseJsonToChannelJsonList(string jsonContent)
        {
            if (jsonContent.StartsWith("\uFEFF")) jsonContent = jsonContent.Substring(1);
            jsonContent = jsonContent.Trim();

            if (jsonContent.StartsWith("{") && jsonContent.EndsWith("]"))
                jsonContent = jsonContent.Substring(0, jsonContent.Length - 1);

            if (jsonContent.StartsWith("{") && !jsonContent.StartsWith("["))
                jsonContent = "[" + jsonContent + "]";

            jsonContent = System.Text.RegularExpressions.Regex.Replace(jsonContent, @",\s*\]", "]");
            jsonContent = System.Text.RegularExpressions.Regex.Replace(jsonContent, @",\s*\}", "}");

            JToken root = JToken.Parse(jsonContent);
            JArray array = null;
            if (root.Type == JTokenType.Array)
                array = (JArray)root;
            else if (root.Type == JTokenType.Object)
            {
                foreach (var prop in ((JObject)root).Properties())
                    if (prop.Value.Type == JTokenType.Array)
                    {
                        array = (JArray)prop.Value;
                        break;
                    }
            }
            if (array == null)
                throw new Exception("No array found in JSON.");

            var result = new List<ChannelJson>();
            foreach (JObject obj in array)
            {
                var ch = new ChannelJson
                {
                    name = obj["name"]?.ToString() ?? "",
                    logo_url = obj["logo_url"]?.ToString() ?? "",
                    group = obj["group"]?.ToString() ?? "",
                    tvg_id = obj["tvg_id"]?.ToString() ?? "",
                    isFavorite = obj["isFavorite"]?.Value<bool>() ?? false,
                    country = obj["country"]?.ToString() ?? "",
                    nanoid = obj["nanoid"]?.ToString() ?? "",
                    isGeoBlocked = obj["isGeoBlocked"]?.Value<bool>() ?? false
                };

                var streamUrlsToken = obj["stream_urls"];
                if (streamUrlsToken?.Type == JTokenType.Array)
                    ch.stream_urls = streamUrlsToken.Select(t => t.ToString()).ToList();
                else if (streamUrlsToken?.Type == JTokenType.String)
                    ch.stream_urls = new List<string> { streamUrlsToken.ToString() };
                else
                    ch.stream_urls = new List<string>();

                if ((ch.stream_urls == null || ch.stream_urls.Count == 0) && obj["url"] != null)
                {
                    string url = obj["url"].ToString();
                    if (!string.IsNullOrEmpty(url))
                        ch.stream_urls = new List<string> { url };
                }

                ch.youtube_urls = obj["youtube_urls"]?.Type == JTokenType.Array
                    ? obj["youtube_urls"].Select(t => t.ToString()).ToList() : new List<string>();
                ch.languages = obj["languages"]?.Type == JTokenType.Array
                    ? obj["languages"].Select(t => t.ToString()).ToList() : new List<string>();

                ch.stream_urls ??= new List<string>();
                ch.youtube_urls ??= new List<string>();
                ch.languages ??= new List<string>();

                result.Add(ch);
            }
            return result;
        }

        // ------------------------------------------------------------------
        // Check Duplicates
        // ------------------------------------------------------------------
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

        // ------------------------------------------------------------------
        // Sorting
        // ------------------------------------------------------------------
        private void ChannelsGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;

            string sortProperty = null;
            if (e.Column.Header?.ToString() == "URL (primary)")
                sortProperty = "PrimaryUrl";
            else if (e.Column.Header?.ToString() == "Logo")
                sortProperty = "logo_url";
            else if (e.Column.Header?.ToString() == "" || e.Column.Header?.ToString() == "✎")
                sortProperty = "name";
            else
            {
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

            foreach (var col in ChannelsGrid.Columns)
            {
                col.SortDirection = null;
            }
            e.Column.SortDirection = direction;
        }

        private void ResetOrderBtn_Click(object sender, RoutedEventArgs e)
        {
            var view = CollectionViewSource.GetDefaultView(ChannelsGrid.ItemsSource);
            if (view != null)
            {
                view.SortDescriptions.Clear();
                foreach (var col in ChannelsGrid.Columns)
                    col.SortDirection = null;
            }
            ChannelsGrid.Items.Refresh();
        }

        // ------------------------------------------------------------------
        // Progress bar helpers
        // ------------------------------------------------------------------
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
        // Details window
        // ------------------------------------------------------------------
        private void ChannelsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
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
            ChannelsGrid.CommitEdit(DataGridEditingUnit.Row, true);
            int index = Channels.IndexOf(channel);
            var detailsWindow = new ChannelDetailsWindow(Channels, index);
            detailsWindow.Owner = this;
            if (detailsWindow.ShowDialog() == true)
            {
                ChannelsGrid.CommitEdit(DataGridEditingUnit.Row, true);
                ChannelsGrid.Items.Refresh();
            }
        }

        // ------------------------------------------------------------------
        // Save As
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