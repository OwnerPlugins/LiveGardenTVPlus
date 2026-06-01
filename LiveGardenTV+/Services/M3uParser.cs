using LiveGardenTVPlus.Models;
using System.IO;
using System.Text.RegularExpressions;

namespace LiveGardenTVPlus.Services
{
    public static class M3uParser
    {
        public static List<Channel> Parse(string filePath)
        {
            var channels = new List<Channel>();
            var lines = File.ReadAllLines(filePath);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("#EXTINF:"))
                {
                    var extinf = lines[i];
                    var url = (i + 1 < lines.Length) ? lines[i + 1] : "";
                    if (string.IsNullOrEmpty(url)) continue;

                    channels.Add(new Channel
                    {
                        Url = url,
                        Name = ExtractName(extinf),
                        Group = ExtractGroup(extinf),
                        Logo = ExtractLogo(extinf),
                        TvgId = ExtractTvgId(extinf)
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