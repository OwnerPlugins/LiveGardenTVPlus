using LiveGardenTVPlus.Models;
using LiveGardenTVPlus.Views;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace LiveGardenTVPlus.Services
{
    public static class JsonImportService
    {
        /// <summary>
        /// Import JSON from file path with mapping window.
        /// Returns list of ChannelJson (for editor) or can convert to Channel (for main window).
        /// </summary>
        public static List<ChannelJson> ImportFromFileWithMapping(string filePath, Window owner)
        {
            string json = File.ReadAllText(filePath, Encoding.UTF8);
            return ImportFromJsonWithMapping(json, filePath, owner);
        }

        /// <summary>
        /// Import JSON from URL content with mapping window.
        /// </summary>
        public static List<ChannelJson> ImportFromUrlWithMapping(string jsonContent, string fileName, Window owner)
        {
            return ImportFromJsonWithMapping(jsonContent, fileName, owner);
        }

        /// <summary>
        /// Core method: shows mapping window and returns mapped channels.
        /// Returns null if user cancels or mapping fails.
        /// </summary>
        public static List<ChannelJson> ImportFromJsonWithMapping(string jsonContent, string fileName, Window owner)
        {
            try
            {
                // Validate JSON before showing mapping window
                if (!IsValidJson(jsonContent))
                {
                    // Try to fix common issues
                    jsonContent = FixJson(jsonContent);
                    if (!IsValidJson(jsonContent))
                        throw new Exception("Invalid JSON format.");
                }

                var mappingWindow = new JsonImportMappingWindow(jsonContent, fileName);
                mappingWindow.Owner = owner;
                if (mappingWindow.ShowDialog() == true)
                {
                    return mappingWindow.GetMappedChannels();
                }
                return null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"JSON import error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        /// <summary>
        /// Convert ChannelJson list to Channel list (for MainWindow).
        /// </summary>
        public static List<Channel> ConvertToChannelList(List<ChannelJson> jsonChannels)
        {
            if (jsonChannels == null) return new List<Channel>();

            return jsonChannels.Select(ch => new Channel
            {
                Name = ch.name ?? "",
                Url = ch.stream_urls?.FirstOrDefault() ?? "",
                Logo = ch.logo_url ?? "",
                Group = ch.group ?? "General",
                TvgId = ch.tvg_id ?? "",
                IsFavorite = ch.isFavorite,
                // Add these only if Channel has them:
                // Country = ch.country,
                // IsGeoBlocked = ch.isGeoBlocked,
                // Languages = ch.languages,
                // Nanoid = ch.nanoid,
                StreamUrls = ch.stream_urls ?? new List<string>(),
                YoutubeUrls = ch.youtube_urls ?? new List<string>()
            }).ToList();
        }

        /// <summary>
        /// Convert ChannelJson list to Channel list (for MainWindow) - alternative without StreamUrls/YoutubeUrls.
        /// </summary>
        public static List<Channel> ConvertToChannelListSimple(List<ChannelJson> jsonChannels)
        {
            if (jsonChannels == null) return new List<Channel>();

            return jsonChannels.Select(ch => new Channel
            {
                Name = ch.name ?? "",
                Url = ch.stream_urls?.FirstOrDefault() ?? "",
                Logo = ch.logo_url ?? "",
                Group = ch.group ?? "General",
                TvgId = ch.tvg_id ?? "",
                IsFavorite = ch.isFavorite
            }).ToList();
        }

        /// <summary>
        /// Quick import without mapping window (for simple JSON structures).
        /// </summary>
        public static List<ChannelJson> ImportDirect(string jsonContent)
        {
            jsonContent = FixJson(jsonContent);
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore
            };
            var result = JsonConvert.DeserializeObject<List<ChannelJson>>(jsonContent, settings);
            if (result == null || result.Count == 0)
            {
                // Try parsing as JArray
                var token = JToken.Parse(jsonContent);
                if (token is JArray array)
                {
                    result = new List<ChannelJson>();
                    foreach (var item in array)
                    {
                        result.Add(item.ToObject<ChannelJson>());
                    }
                }
            }
            return result ?? new List<ChannelJson>();
        }

        private static bool IsValidJson(string json)
        {
            try
            {
                JToken.Parse(json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string FixJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return json;

            // Remove BOM
            if (json.StartsWith("\uFEFF"))
                json = json.Substring(1);

            json = json.Trim();

            // Wrap multiple objects without array
            if (json.StartsWith("{") && !json.StartsWith("["))
            {
                // Check if it's a single object or multiple concatenated
                int openBraces = 0;
                bool isMultiple = false;
                for (int i = 0; i < json.Length; i++)
                {
                    if (json[i] == '{') openBraces++;
                    else if (json[i] == '}') openBraces--;
                    else if (json[i] == ',' && openBraces == 0 && i < json.Length - 1)
                    {
                        isMultiple = true;
                        break;
                    }
                }
                if (isMultiple)
                    json = "[" + json + "]";
            }

            // Remove trailing commas
            json = Regex.Replace(json, @",\s*\]", "]");
            json = Regex.Replace(json, @",\s*\}", "}");

            return json;
        }
    }
}