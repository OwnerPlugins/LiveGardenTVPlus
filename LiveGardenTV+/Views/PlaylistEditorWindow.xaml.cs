using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using LiveGardenTVPlus.Models;
using LiveGardenTVPlus.Services;

namespace LiveGardenTVPlus.Views
{
    public partial class PlaylistEditorWindow : Window
    {
        public ObservableCollection<Channel> Channels { get; set; }
        public bool IsSaved { get; private set; }
        public string SavedFilePath { get; private set; }

        public PlaylistEditorWindow(System.Collections.Generic.List<Channel> channels)
        {
            InitializeComponent();
            Channels = new ObservableCollection<Channel>(channels);
            ChannelsGrid.ItemsSource = Channels;
            IsSaved = false;
            SavedFilePath = null;
        }

        // -------------------- Group Management --------------------
        private void AddGroupBtn_Click(object sender, RoutedEventArgs e)
        {
            string groupName = Microsoft.VisualBasic.Interaction.InputBox("Enter new group name:", "Add Group", "");
            if (!string.IsNullOrEmpty(groupName))
            {
                foreach (var ch in Channels)
                {
                    if (string.IsNullOrEmpty(ch.Group))
                        ch.Group = groupName;
                }
                ChannelsGrid.Items.Refresh();
            }
        }

        private void RenameGroupBtn_Click(object sender, RoutedEventArgs e)
        {
            var selected = ChannelsGrid.SelectedItem as Channel;
            if (selected == null)
            {
                MessageBox.Show("Select a channel from the group you want to rename.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string oldGroup = selected.Group;
            string newGroup = Microsoft.VisualBasic.Interaction.InputBox($"Rename group '{oldGroup}' to:", "Rename Group", oldGroup);
            if (!string.IsNullOrEmpty(newGroup) && newGroup != oldGroup)
            {
                foreach (var ch in Channels.Where(c => c.Group == oldGroup))
                    ch.Group = newGroup;
                ChannelsGrid.Items.Refresh();
            }
        }

        private void DeleteGroupBtn_Click(object sender, RoutedEventArgs e)
        {
            var selected = ChannelsGrid.SelectedItem as Channel;
            if (selected == null)
            {
                MessageBox.Show("Select a channel from the group you want to delete.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string groupToDelete = selected.Group;
            if (MessageBox.Show($"Delete all channels in group '{groupToDelete}'? This cannot be undone.", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                var toRemove = Channels.Where(c => c.Group == groupToDelete).ToList();
                foreach (var ch in toRemove)
                    Channels.Remove(ch);
            }
        }

        // -------------------- URL Check --------------------
        private async void CheckUrlsBtn_Click(object sender, RoutedEventArgs e)
        {
            CheckUrlsBtn.IsEnabled = false;
            var progress = new Progress<KeyValuePair<Channel, bool>>(UpdateUrlStatus);
            await Task.Run(() => CheckAllUrls(progress));
            CheckUrlsBtn.IsEnabled = true;
            MessageBox.Show("URL check completed.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExportFailedBtn_Click(object sender, RoutedEventArgs e)
        {
            var failed = Channels.Where(c => c.UrlStatus == "FAIL").ToList();
            if (failed.Count == 0)
            {
                MessageBox.Show("No failed channels to export.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            ExportChannelsToM3u(failed, "failed_channels.m3u");
        }

        private void ExportSuccessBtn_Click(object sender, RoutedEventArgs e)
        {
            var working = Channels.Where(c => c.UrlStatus == "OK").ToList();
            if (working.Count == 0)
            {
                MessageBox.Show("No working channels to export.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            ExportChannelsToM3u(working, "working_channels.m3u");
        }

        private void ExportChannelsToM3u(List<Channel> channels, string defaultFileName)
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
                        writer.WriteLine($"#EXTINF:-1 group-title=\"{ch.Group}\" tvg-logo=\"{ch.Logo}\" tvg-id=\"{ch.TvgId}\",{ch.Name}");
                        writer.WriteLine(ch.Url);
                    }
                }
                MessageBox.Show($"Exported {channels.Count} channels to {dialog.FileName}", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void UpdateUrlStatus(KeyValuePair<Channel, bool> result)
        {
            result.Key.UrlStatus = result.Value ? "OK" : "FAIL";
            // Force refresh of the row
            ChannelsGrid.Items.Refresh();
        }

        private void CheckAllUrls(IProgress<KeyValuePair<Channel, bool>> progress)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(5);
                foreach (var channel in Channels)
                {
                    bool isOk = false;
                    try
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, channel.Url);
                        request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
                        var response = client.Send(request);
                        isOk = response.IsSuccessStatusCode;
                        if (!isOk)
                        {
                            var fullRequest = new HttpRequestMessage(HttpMethod.Get, channel.Url);
                            var fullResponse = client.Send(fullRequest, HttpCompletionOption.ResponseHeadersRead);
                            isOk = fullResponse.IsSuccessStatusCode;
                        }
                    }
                    catch { isOk = false; }
                    progress.Report(new KeyValuePair<Channel, bool>(channel, isOk));
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
                ExportToM3u(dialog.FileName);
                SavedFilePath = dialog.FileName;
                IsSaved = true;
                MessageBox.Show($"Playlist saved to {dialog.FileName}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ExportToM3u(string filePath)
        {
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("#EXTM3U");
                foreach (var ch in Channels)
                {
                    writer.WriteLine($"#EXTINF:-1 group-title=\"{ch.Group}\" tvg-logo=\"{ch.Logo}\" tvg-id=\"{ch.TvgId}\",{ch.Name}");
                    writer.WriteLine(ch.Url);
                }
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}