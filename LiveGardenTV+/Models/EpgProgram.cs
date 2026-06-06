using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using LiveGardenTVPlus.Models;
using System.Text.RegularExpressions;

namespace LiveGardenTVPlus.Services
{
    public class EpgService
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private List<EpgChannel> _channels = new List<EpgChannel>();
        private Dictionary<string, string> _channelMapping = new Dictionary<string, string>(); // M3U channel name -> EPG channel Id
        public bool HasAnyChannel() => _channels.Count > 0;

        public async Task LoadEpgAsync(string epgUrl)
        {
            if (string.IsNullOrEmpty(epgUrl))
                return;

            Debug.WriteLine($"EPG loading started for {epgUrl}");

            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(30);
                var response = await client.GetAsync(epgUrl);
                response.EnsureSuccessStatusCode();

                Debug.WriteLine($"EPG downloaded, content length: {response.Content.Headers.ContentLength}");

                string tempFile = Path.Combine(Path.GetTempPath(), "epg_test.xml");
                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = File.Create(tempFile))
                {
                    if (epgUrl.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                    {
                        using (var gzipStream = new GZipStream(contentStream, CompressionMode.Decompress))
                            await gzipStream.CopyToAsync(fileStream);
                    }
                    else
                    {
                        await contentStream.CopyToAsync(fileStream);
                    }
                }
                Debug.WriteLine($"EPG saved to {tempFile}");

                // Parse the saved file
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
                using (var fileStream = File.OpenRead(tempFile))
                using (var reader = XmlReader.Create(fileStream, settings))
                {
                    EpgChannel currentChannel = null;
                    EpgProgramme currentProgram = null;

                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            switch (reader.Name)
                            {
                                case "channel":
                                    string id = reader.GetAttribute("id");
                                    currentChannel = new EpgChannel { Id = id };
                                    _channels.Add(currentChannel);
                                    break;
                                case "display-name":
                                    if (currentChannel != null && reader.Read())
                                        currentChannel.DisplayName = reader.Value.Trim();
                                    break;
                                case "icon":
                                    if (currentChannel != null)
                                        currentChannel.Icon = reader.GetAttribute("src");
                                    break;
                                case "programme":
                                    string startStr = reader.GetAttribute("start");
                                    string stopStr = reader.GetAttribute("stop");
                                    string channelId = reader.GetAttribute("channel");
                                    currentProgram = new EpgProgramme
                                    {
                                        Start = ParseEpgDate(startStr),
                                        Stop = ParseEpgDate(stopStr)
                                    };
                                    var channel = _channels.FirstOrDefault(c => c.Id == channelId);
                                    if (channel != null)
                                        channel.Programmes.Add(currentProgram);
                                    break;
                                case "title":
                                    if (currentProgram != null && reader.Read())
                                        currentProgram.Title = reader.Value.Trim();
                                    break;
                                case "desc":
                                    if (currentProgram != null && reader.Read())
                                        currentProgram.Description = reader.Value.Trim();
                                    break;
                                case "category":
                                    if (currentProgram != null && reader.Read())
                                        currentProgram.Category = reader.Value.Trim();
                                    break;
                            }
                        }
                    }
                }

                Debug.WriteLine("EPG parsing completed successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EPG load error: {ex.Message}");
            }
            

        }

        public bool HasChannel(string channelId)
        {
            return _channels.Any(c => c.Id == channelId);
        }

         public string GetMappedEpgId(string channelName)
        {
            return GetBestMatchingEpgChannel(channelName);
        }

        public async Task<List<EpgProgramme>> GetProgramsForChannelAsync(string channelId, DateTime startUtc, DateTime endUtc)
        {
            var channel = _channels.FirstOrDefault(c => c.Id == channelId);
            if (channel == null) return new List<EpgProgramme>();
            return channel.Programmes
                .Where(p => p.Start < endUtc && p.Stop > startUtc)
                .ToList();
        }

        private DateTime ParseEpgDate(string dateStr)
        {
            if (string.IsNullOrEmpty(dateStr))
                return DateTime.MinValue;

            // Example: "20260303205100 +0200" or "20260303205100"
            var parts = dateStr.Trim().Split(' ');
            string datePart = parts[0];
            if (datePart.Length < 14)
                return DateTime.MinValue;

            int year = int.Parse(datePart.Substring(0, 4));
            int month = int.Parse(datePart.Substring(4, 2));
            int day = int.Parse(datePart.Substring(6, 2));
            int hour = int.Parse(datePart.Substring(8, 2));
            int minute = int.Parse(datePart.Substring(10, 2));
            int second = int.Parse(datePart.Substring(12, 2));

            DateTime local = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);

            if (parts.Length > 1 && parts[1].Length >= 5)
            {
                // Offset format: +0200 or -0500
                string offsetStr = parts[1];
                int sign = offsetStr[0] == '+' ? 1 : -1;
                int offHours = int.Parse(offsetStr.Substring(1, 2));
                int offMins = int.Parse(offsetStr.Substring(3, 2));
                TimeSpan offset = new TimeSpan(sign * offHours, sign * offMins, 0);
                DateTimeOffset dto = new DateTimeOffset(local, offset);
                return dto.UtcDateTime;
            }
            else
            {
                // No offset: assume UTC
                return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
            }
        }

        public EpgProgramme GetCurrentProgram(string channelName, string tvgId, DateTime nowUtc)
        {
            // Try first by tvgId
            if (!string.IsNullOrEmpty(tvgId))
            {
                var channel = _channels.FirstOrDefault(c => c.Id == tvgId);
                if (channel != null)
                    return channel.Programmes.FirstOrDefault(p => p.Start <= nowUtc && p.Stop > nowUtc);
            }

            // Try fuzzy matching by channel name
            string epgChannelId = GetBestMatchingEpgChannel(channelName);
            if (!string.IsNullOrEmpty(epgChannelId))
            {
                var channel = _channels.FirstOrDefault(c => c.Id == epgChannelId);
                if (channel != null)
                    return channel.Programmes.FirstOrDefault(p => p.Start <= nowUtc && p.Stop > nowUtc);
            }

            return null;
        }

        private string GetBestMatchingEpgChannel(string channelName)
        {
            if (string.IsNullOrEmpty(channelName)) return null;

            // Check cache
            if (_channelMapping.TryGetValue(channelName, out var cached))
                return cached;

            // Fuzzy matching logic
            var bestMatch = _channels
                .Select(c => new { Channel = c, Similarity = ComputeSimilarity(channelName, c.DisplayName) })
                .Where(x => x.Similarity > 0.6) // threshold
                .OrderByDescending(x => x.Similarity)
                .FirstOrDefault();

            if (bestMatch != null)
            {
                _channelMapping[channelName] = bestMatch.Channel.Id;
                return bestMatch.Channel.Id;
            }

            return null;
        }

        private double ComputeSimilarity(string source, string target)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target)) return 0;
            source = source.ToLowerInvariant();
            target = target.ToLowerInvariant();

            // Remove common prefixes/suffixes (e.g., "Rai 1" vs "Rai1")
            source = Regex.Replace(source, @"[^\w]", "");
            target = Regex.Replace(target, @"[^\w]", "");

            // Levenshtein distance based similarity
            int maxLen = Math.Max(source.Length, target.Length);
            if (maxLen == 0) return 1;
            int distance = LevenshteinDistance(source, target);
            return 1.0 - (double)distance / maxLen;
        }

        private int LevenshteinDistance(string s, string t)
        {
            int n = s.Length, m = t.Length;
            int[,] d = new int[n + 1, m + 1];
            if (n == 0) return m;
            if (m == 0) return n;
            for (int i = 0; i <= n; d[i, 0] = i++) ;
            for (int j = 0; j <= m; d[0, j] = j++) ;
            for (int i = 1; i <= n; i++)
                for (int j = 1; j <= m; j++)
                {
                    int cost = (s[i - 1] == t[j - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            return d[n, m];
        }
    }
}