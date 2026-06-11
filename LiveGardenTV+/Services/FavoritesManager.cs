using LiveGardenTVPlus.Models;
using Newtonsoft.Json;
using System.IO;

namespace LiveGardenTVPlus.Services
{
    public static class FavoritesManager
    {
        private static readonly string FavoritesFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "favorites.json");

        public static void SaveFavorites(IEnumerable<Channel> channels)
        {
            var urls = channels.Where(c => c.IsFavorite).Select(c => c.Url).ToList();
            File.WriteAllText(FavoritesFile, JsonConvert.SerializeObject(urls, Formatting.Indented));
        }

        public static HashSet<string> LoadFavoriteUrls()
        {
            if (!File.Exists(FavoritesFile)) return new HashSet<string>();
            var list = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(FavoritesFile));
            return new HashSet<string>(list ?? new List<string>());
        }

        public static void ApplyFavorites(IEnumerable<Channel> channels)
        {
            if (!File.Exists(FavoritesFile)) return;
            var favUrls = LoadFavoriteUrls();
            if (favUrls == null) return;

            var normalizedFavs = favUrls.Select(u => NormalizeUrl(u)).ToHashSet();

            foreach (var c in channels)
            {
                string normalizedChannelUrl = NormalizeUrl(c.Url);
                c.IsFavorite = normalizedFavs.Contains(normalizedChannelUrl);
            }
        }

        private static string NormalizeUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            string result = url.ToLowerInvariant();
            result = result.TrimEnd('/');
            return result;
        }
    }
}