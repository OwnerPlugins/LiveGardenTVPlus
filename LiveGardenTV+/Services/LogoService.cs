using LiveGardenTVPlus.Models;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace LiveGardenTVPlus.Services
{
    public class LogoService
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private Dictionary<string, string> _logoDict = new Dictionary<string, string>();
        private readonly string _cacheFile = Path.Combine(Path.GetTempPath(), "LiveGardenTVPlus_logos.json");

        public async Task<bool> LoadLogosFromIndex(string indexUrl, string subFolder, bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!forceRefresh && File.Exists(_cacheFile))
                {
                    var json = await File.ReadAllTextAsync(_cacheFile, cancellationToken);
                    var cached = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (cached != null && cached.Count > 0)
                    {
                        _logoDict = cached;
                        if (!string.IsNullOrEmpty(subFolder) && subFolder != "ALL")
                        {
                            _logoDict = _logoDict
                                .Where(kvp => kvp.Value.Contains($"/logos/{subFolder}/", StringComparison.OrdinalIgnoreCase))
                                .ToDictionary(k => k.Key, k => k.Value);
                        }
                        return true;
                    }
                }

                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("LiveGardenTVPlus/1.0");
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(30));
                string content = await _httpClient.GetStringAsync(indexUrl, cts.Token);
                var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                var tempDict = new Dictionary<string, string>();
                string baseUrl = "https://raw.githubusercontent.com/OwnerPlugins/logos/main/";

                foreach (var line in lines)
                {
                    if (!line.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
                    string fileName = Path.GetFileNameWithoutExtension(line);
                    string normalized = NormalizeName(fileName);
                    if (string.IsNullOrEmpty(normalized)) continue;
                    string fullUrl = baseUrl + line.Replace("\\", "/");
                    if (!tempDict.ContainsKey(normalized))
                        tempDict[normalized] = fullUrl;
                }

                var jsonOut = JsonSerializer.Serialize(tempDict);
                await File.WriteAllTextAsync(_cacheFile, jsonOut, cancellationToken);

                _logoDict = tempDict;
                if (!string.IsNullOrEmpty(subFolder) && subFolder != "ALL")
                {
                    _logoDict = _logoDict
                        .Where(kvp => kvp.Value.Contains($"/logos/{subFolder}/", StringComparison.OrdinalIgnoreCase))
                        .ToDictionary(k => k.Key, k => k.Value);
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LogoService error: {ex.Message}");
                return false;
            }
        }

        private string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            string normalized = RemoveDiacritics(name);
            return normalized.ToLowerInvariant()
                .Replace(" ", "").Replace("-", "").Replace("_", "")
                .Replace("hd", "").Replace("sd", "").Replace("plus", "").Replace("music", "")
                .Trim();
        }

        private string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in normalizedString)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        public string FindBestMatch(string channelName, string tvgId, double? threshold = null)
        {
            // Use saved preference if threshold not explicitly provided
            double actualThreshold = threshold ?? UserPreferences.Load().LogoMatchingThreshold;

            if (!string.IsNullOrEmpty(tvgId))
            {
                string normTvg = NormalizeName(tvgId);
                if (_logoDict.TryGetValue(normTvg, out string url))
                    return url;
            }

            string normChannel = NormalizeName(channelName);
            var candidates = new List<KeyValuePair<string, double>>();
            foreach (var kv in _logoDict)
            {
                double similarity = ComputeSimilarity(normChannel, kv.Key);
                if (similarity >= actualThreshold)
                    candidates.Add(new KeyValuePair<string, double>(kv.Key, similarity));
            }
            if (candidates.Count > 0)
            {
                var best = candidates.OrderByDescending(kv => kv.Value).First();
                return _logoDict[best.Key];
            }
            return null;
        }

        private double ComputeSimilarity(string s1, string s2)
        {
            int maxLen = Math.Max(s1.Length, s2.Length);
            if (maxLen == 0) return 1.0;
            int distance = LevenshteinDistance(s1, s2);
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

        public List<LogoInfo> GetAllLogos()
        {
            return _logoDict.Select(kv => new LogoInfo { Name = kv.Key, Url = kv.Value }).ToList();
        }
    }
}