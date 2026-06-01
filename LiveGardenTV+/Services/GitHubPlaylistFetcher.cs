using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LiveGardenTVPlus.Services
{
    public static class GitHubPlaylistFetcher
    {
        public static async Task<List<PlaylistInfo>> GetM3uPlaylistsAsync()
        {
            var playlists = new List<PlaylistInfo>();
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "LiveGardenTVPlus");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

            try
            {
                string rootUrl = "https://api.github.com/repos/OwnerPlugins/TivuStreamList/contents/ios?ref=list";
                await AddPlaylistsFromUrl(client, rootUrl, "", playlists);

                string localUrl = "https://api.github.com/repos/OwnerPlugins/TivuStreamList/contents/ios/local?ref=list";
                await AddPlaylistsFromUrl(client, localUrl, "local/", playlists);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GitHub fetch error: {ex.Message}");
                return GetFallbackPlaylists();
            }

            if (playlists.Count == 0)
                return GetFallbackPlaylists();

            return playlists;
        }

        private static async Task AddPlaylistsFromUrl(HttpClient client, string apiUrl, string prefix, List<PlaylistInfo> playlists)
        {
            var response = await client.GetAsync(apiUrl);
            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to fetch {apiUrl}: {response.StatusCode}");
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var items = JsonSerializer.Deserialize<List<GitHubItem>>(json, options);
            if (items == null) return;

            foreach (var item in items)
            {
                if (item.Type == "file" && item.Name.EndsWith(".m3u", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(item.DownloadUrl))
                {
                    string displayName = prefix + item.Name;
                    playlists.Add(new PlaylistInfo { DisplayName = displayName, RawUrl = item.DownloadUrl });
                    System.Diagnostics.Debug.WriteLine($"Added: {displayName} -> {item.DownloadUrl}");
                }
            }
        }

        private static List<PlaylistInfo> GetFallbackPlaylists()
        {
            return new List<PlaylistInfo>
            {
                new PlaylistInfo { DisplayName = "Playlist (Default)", RawUrl = "https://raw.githubusercontent.com/OwnerPlugins/TivuStreamList/refs/heads/list/ios/playlist.m3u" },
                new PlaylistInfo { DisplayName = "Italy (ita_list)", RawUrl = "https://raw.githubusercontent.com/OwnerPlugins/TivuStreamList/refs/heads/list/ios/ita_list.m3u" },
                new PlaylistInfo { DisplayName = "Italy Regionali (italia_regionali)", RawUrl = "https://raw.githubusercontent.com/OwnerPlugins/TivuStreamList/refs/heads/list/ios/italia_regionali.m3u" },
                new PlaylistInfo { DisplayName = "Sports (sports_list)", RawUrl = "https://raw.githubusercontent.com/OwnerPlugins/TivuStreamList/refs/heads/list/ios/sports_list.m3u" },
                new PlaylistInfo { DisplayName = "News (news_list)", RawUrl = "https://raw.githubusercontent.com/OwnerPlugins/TivuStreamList/refs/heads/list/ios/news_list.m3u" },
                new PlaylistInfo { DisplayName = "Music (music_list)", RawUrl = "https://raw.githubusercontent.com/OwnerPlugins/TivuStreamList/refs/heads/list/ios/music_list.m3u" },
                new PlaylistInfo { DisplayName = "Kids (kids_list)", RawUrl = "https://raw.githubusercontent.com/OwnerPlugins/TivuStreamList/refs/heads/list/ios/kids_list.m3u" },
                new PlaylistInfo { DisplayName = "Movies (movies_list)", RawUrl = "https://raw.githubusercontent.com/OwnerPlugins/TivuStreamList/refs/heads/list/ios/movies_list.m3u" },
                new PlaylistInfo { DisplayName = "Animation (animation_list)", RawUrl = "https://raw.githubusercontent.com/OwnerPlugins/TivuStreamList/refs/heads/list/ios/animation_list.m3u" },
                new PlaylistInfo { DisplayName = "Estero (estero_list)", RawUrl = "https://raw.githubusercontent.com/OwnerPlugins/TivuStreamList/refs/heads/list/ios/estero_list.m3u" },
                new PlaylistInfo { DisplayName = "Family (family_list)", RawUrl = "https://raw.githubusercontent.com/OwnerPlugins/TivuStreamList/refs/heads/list/ios/family_list.m3u" },
                new PlaylistInfo { DisplayName = "HbbTV (hbbtv_list)", RawUrl = "https://raw.githubusercontent.com/OwnerPlugins/TivuStreamList/refs/heads/list/ios/hbbtv_list.m3u" },
                new PlaylistInfo { DisplayName = "Plex TV (plex-tv)", RawUrl = "https://raw.githubusercontent.com/OwnerPlugins/TivuStreamList/refs/heads/list/ios/plex-tv.m3u" },
                new PlaylistInfo { DisplayName = "Pluto Live IT (pluto_live_it)", RawUrl = "https://raw.githubusercontent.com/OwnerPlugins/TivuStreamList/refs/heads/list/ios/pluto_live_it.m3u" },
                new PlaylistInfo { DisplayName = "Pluto VOD IT (pluto_vod_it)", RawUrl = "https://raw.githubusercontent.com/OwnerPlugins/TivuStreamList/refs/heads/list/ios/pluto_vod_it.m3u" },
                new PlaylistInfo { DisplayName = "Radio Country (radio_country)", RawUrl = "https://raw.githubusercontent.com/OwnerPlugins/TivuStreamList/refs/heads/list/ios/radio_country.m3u" },
                new PlaylistInfo { DisplayName = "Radio Genre (radio_genre)", RawUrl = "https://raw.githubusercontent.com/OwnerPlugins/TivuStreamList/refs/heads/list/ios/radio_genre.m3u" },
                new PlaylistInfo { DisplayName = "Radio List (radio_list)", RawUrl = "https://raw.githubusercontent.com/OwnerPlugins/TivuStreamList/refs/heads/list/ios/radio_list.m3u" },
                new PlaylistInfo { DisplayName = "Rai Med (rai_med_list)", RawUrl = "https://raw.githubusercontent.com/OwnerPlugins/TivuStreamList/refs/heads/list/ios/rai_med_list.m3u" },
                new PlaylistInfo { DisplayName = "Rakuten IT (rakuten_it)", RawUrl = "https://raw.githubusercontent.com/OwnerPlugins/TivuStreamList/refs/heads/list/ios/rakuten_it.m3u" },
                new PlaylistInfo { DisplayName = "Relax (relax_list)", RawUrl = "https://raw.githubusercontent.com/OwnerPlugins/TivuStreamList/refs/heads/list/ios/relax_list.m3u" },
                new PlaylistInfo { DisplayName = "Samsung Plus (Samsung_plus)", RawUrl = "https://raw.githubusercontent.com/OwnerPlugins/TivuStreamList/refs/heads/list/ios/Samsung_plus.m3u" },
                new PlaylistInfo { DisplayName = "Webcam (webcam_list)", RawUrl = "https://raw.githubusercontent.com/OwnerPlugins/TivuStreamList/refs/heads/list/ios/webcam_list.m3u" }
            };
        }

        private class GitHubItem
        {
            public string Name { get; set; } = "";
            public string Type { get; set; } = "";
            [JsonPropertyName("download_url")]
            public string DownloadUrl { get; set; } = "";
        }
    }

    public class PlaylistInfo
    {
        public string DisplayName { get; set; } = "";
        public string RawUrl { get; set; } = "";
    }
}