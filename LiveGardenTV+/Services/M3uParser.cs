using LiveGardenTVPlus.Models;
using System.IO;
using System.Text.RegularExpressions;


namespace LiveGardenTVPlus.Services
{
    public static class M3uParser
    {
        public static string EpgUrl { get; private set; }

        public static List<Channel> Parse(string filePath)
        {
            EpgUrl = null;
            var channels = new List<Channel>();
            var lines = File.ReadAllLines(filePath);

            // Extract EPG URL from first line if present
            if (lines.Length > 0 && lines[0].StartsWith("#EXTM3U"))
            {
                var match = Regex.Match(lines[0], @"x-tvg-url=""([^""]+)""");
                if (match.Success)
                    EpgUrl = match.Groups[1].Value;
            }

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("#EXTINF:"))
                {
                    var extinf = lines[i];
                    string url = null;
                    // Search URLs skipping blank lines or lines starting with '#'
                    for (int j = i + 1; j < lines.Length; j++)
                    {
                        string line = lines[j].Trim();
                        if (string.IsNullOrEmpty(line)) continue;
                        if (line.StartsWith("#")) continue;
                        url = line;
                        i = j;
                        break;
                    }
                    if (string.IsNullOrEmpty(url)) continue;

                    string name = ExtractName(extinf);
                    string group = ExtractGroup(extinf);

                    // Detect radio channel based ONLY on URL extension
                    bool isRadio = false;
                    if (!string.IsNullOrEmpty(url))
                    {
                        string ext = Path.GetExtension(url).ToLower();
                        isRadio = ext == ".mp3" || ext == ".aac" || ext == ".ogg" || ext == ".m4a" ||
                                  ext == ".wma" || ext == ".flac" || ext == ".opus" || ext == ".wav";
                    }

                    channels.Add(new Channel
                    {
                        Url = url,
                        Name = name,
                        Group = group,
                        Logo = ExtractLogo(extinf),
                        TvgId = ExtractTvgId(extinf),
                        IsRadio = isRadio
                    });
                }
            }
            return channels;
        }

        private static string ExtractName(string line)
        {
            var parts = line.Split(',');
            return parts.Length > 1 ? parts.Last().Trim() : "Unknown";
        }

        private static string ExtractGroup(string line)
        {
            var match = Regex.Match(line, @"group-title=""([^""]+)""");
            return match.Success ? match.Groups[1].Value : "General";
        }

        private static string ExtractLogo(string line)
        {
            var match = Regex.Match(line, @"tvg-logo=""([^""]+)""");
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string ExtractTvgId(string line)
        {
            var match = Regex.Match(line, @"tvg-id=""([^""]+)""");
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}