using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace LiveGardenTVPlus.Services
{
    public static class ImageCache
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly string _cacheFolder = Path.Combine(Path.GetTempPath(), "LiveGardenTVPlus_LogoCache");

        static ImageCache()
        {
            Directory.CreateDirectory(_cacheFolder);
        }

        public static async Task<BitmapImage> GetImageAsync(string url)
        {
            string fileName = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(url))
                .Replace('/', '_').Replace('+', '-');
            string cacheFile = Path.Combine(_cacheFolder, fileName + ".png");

            if (File.Exists(cacheFile))
                return LoadFromFile(cacheFile);

            try
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("LiveGardenTVPlus/1.0");
                byte[] data = await _httpClient.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(cacheFile, data);
                return LoadFromFile(cacheFile);
            }
            catch
            {
                return null;
            }
        }

        private static BitmapImage LoadFromFile(string path)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
    }
}