using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LiveGardenTVPlus.Models;

namespace LiveGardenTVPlus.Services
{
    public enum CompareField
    {
        Name,
        Url,
        TvgId,
        Logo,
        Group,
        Country,
        Languages,
        Nanoid
    }

    public class CompareResult
    {
        public List<ChannelJson> OnlyInFirst { get; set; } = new List<ChannelJson>();
        public List<ChannelJson> OnlyInSecond { get; set; } = new List<ChannelJson>();
        public List<ChannelJson> InBoth { get; set; } = new List<ChannelJson>();
        public List<ChannelJson> DuplicatesInFirst { get; set; } = new List<ChannelJson>();
        public List<ChannelJson> DuplicatesInSecond { get; set; } = new List<ChannelJson>();
    }

    public static class PlaylistComparer
    {
        // ------------------------------------------------------------------
        // Single‑field comparison (used when a specific field is selected)
        // ------------------------------------------------------------------
        public static CompareResult Compare(List<ChannelJson> first, List<ChannelJson> second, CompareField field)
        {
            var result = new CompareResult();
            first ??= new List<ChannelJson>();
            second ??= new List<ChannelJson>();

            // --- SPECIAL LOGIC FOR URL COMPARISON ---
            if (field == CompareField.Url)
            {
                // For each channel in second, check if ANY URL matches ANY URL in first
                var usedInFirst = new HashSet<ChannelJson>();

                foreach (var chSecond in second)
                {
                    bool foundMatch = false;

                    foreach (var chFirst in first)
                    {
                        if (usedInFirst.Contains(chFirst))
                            continue;

                        if (UrlsMatch(chFirst, chSecond))
                        {
                            result.InBoth.Add(chFirst);
                            result.InBoth.Add(chSecond);
                            usedInFirst.Add(chFirst);
                            foundMatch = true;
                            break;
                        }
                    }

                    if (!foundMatch)
                        result.OnlyInSecond.Add(chSecond);
                }

                // Add any first channels not matched
                foreach (var chFirst in first)
                {
                    if (!usedInFirst.Contains(chFirst))
                        result.OnlyInFirst.Add(chFirst);
                }

                return result;
            }

            // --- STANDARD SINGLE‑FIELD COMPARISON FOR OTHER FIELDS ---
            var firstKeys = first.GroupBy(c => GetKey(c, field)).ToDictionary(g => g.Key, g => g.ToList());
            var secondKeys = second.GroupBy(c => GetKey(c, field)).ToDictionary(g => g.Key, g => g.ToList());

            result.DuplicatesInFirst = firstKeys.Where(kvp => kvp.Value.Count > 1).SelectMany(kvp => kvp.Value).ToList();
            result.DuplicatesInSecond = secondKeys.Where(kvp => kvp.Value.Count > 1).SelectMany(kvp => kvp.Value).ToList();

            var allKeys = new HashSet<string>(firstKeys.Keys);
            allKeys.UnionWith(secondKeys.Keys);

            foreach (var key in allKeys)
            {
                bool inFirst = firstKeys.ContainsKey(key);
                bool inSecond = secondKeys.ContainsKey(key);

                if (inFirst && inSecond)
                {
                    result.InBoth.Add(firstKeys[key].First());
                    result.InBoth.Add(secondKeys[key].First());
                }
                else if (inFirst)
                    result.OnlyInFirst.AddRange(firstKeys[key]);
                else if (inSecond)
                    result.OnlyInSecond.AddRange(secondKeys[key]);
            }

            return result;
        }

        // ------------------------------------------------------------------
        // Priority‑based comparison (Url → Name → TvgId → Logo)
        // ------------------------------------------------------------------
        public static CompareResult CompareWithPriority(List<ChannelJson> first, List<ChannelJson> second)
        {
            var result = new CompareResult();
            first ??= new List<ChannelJson>();
            second ??= new List<ChannelJson>();

            var firstDict = new Dictionary<string, ChannelJson>();
            var secondDict = new Dictionary<string, ChannelJson>();

            foreach (var ch in first)
            {
                string key = BuildCompositeKey(ch);
                if (!firstDict.ContainsKey(key))
                    firstDict[key] = ch;
            }

            foreach (var ch in second)
            {
                string key = BuildCompositeKey(ch);
                if (!secondDict.ContainsKey(key))
                    secondDict[key] = ch;
            }

            var allKeys = new HashSet<string>(firstDict.Keys);
            allKeys.UnionWith(secondDict.Keys);

            foreach (var key in allKeys)
            {
                bool inFirst = firstDict.ContainsKey(key);
                bool inSecond = secondDict.ContainsKey(key);

                if (inFirst && inSecond)
                {
                    result.InBoth.Add(firstDict[key]);
                    result.InBoth.Add(secondDict[key]);
                }
                else if (inFirst)
                    result.OnlyInFirst.Add(firstDict[key]);
                else if (inSecond)
                    result.OnlyInSecond.Add(secondDict[key]);
            }

            return result;
        }

        // ------------------------------------------------------------------
        // Key extraction for a given field
        // ------------------------------------------------------------------
        private static string GetKey(ChannelJson channel, CompareField field)
        {
            switch (field)
            {
                case CompareField.Name: return NormalizeName(channel.name ?? "").ToLowerInvariant();
                case CompareField.Url: return GetNormalizedUrlKey(channel);
                case CompareField.TvgId: return (channel.tvg_id ?? "").Trim().ToLowerInvariant();
                case CompareField.Logo: return (channel.logo_url ?? "").Trim().ToLowerInvariant();
                case CompareField.Group: return (channel.group ?? "").Trim().ToLowerInvariant();
                case CompareField.Country: return (channel.country ?? "").Trim().ToLowerInvariant();
                case CompareField.Languages:
                    var langs = (channel.languages ?? new List<string>()).OrderBy(l => l);
                    return string.Join(";", langs).ToLowerInvariant();
                case CompareField.Nanoid: return (channel.nanoid ?? "").Trim().ToLowerInvariant();
                default: return NormalizeName(channel.name ?? "").ToLowerInvariant();
            }
        }

        // ------------------------------------------------------------------
        // Normalized URL key: all stream URLs simplified and sorted
        // ------------------------------------------------------------------
        private static string GetNormalizedUrlKey(ChannelJson channel)
        {
            if (channel.stream_urls == null || channel.stream_urls.Count == 0)
                return "";

            var normalized = channel.stream_urls
                .Select(SimplifyUrl)
                .Where(u => !string.IsNullOrEmpty(u))
                .Distinct()
                .OrderBy(u => u)
                .ToList();

            return normalized.Count > 0 ? string.Join(";", normalized) : "";
        }

        // ------------------------------------------------------------------
        // Simplify URL: remove protocol, domain, extensions, common segments
        // ------------------------------------------------------------------
        private static string SimplifyUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            url = url.ToLowerInvariant();
            url = Regex.Replace(url, @"^https?://", "");
            url = Regex.Replace(url, @"^[^/]+/", "");          // remove domain
            url = Regex.Replace(url, @"\.m3u8?$", "");          // remove .m3u8/.m3u
            url = Regex.Replace(url, @"/(playlist|livestream|smil:[^/]+)", "");
            url = url.TrimEnd('/');

            return string.IsNullOrEmpty(url) ? null : url;
        }

        // ------------------------------------------------------------------
        // Normalize name: strip quality indicators like (1080p), [HD], etc.
        // ------------------------------------------------------------------
        private static string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            name = Regex.Replace(name, @"\s*[\(\[]\s*(1080p|720p|540p|HD|SD|Not 24/7|24/7|Full HD|4K|UHD)[\)\]]", "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"\s+", " ").Trim();
            return name;
        }

        // ------------------------------------------------------------------
        // Composite key for priority: try Url → Name → TvgId → Logo
        // ------------------------------------------------------------------
        private static string BuildCompositeKey(ChannelJson ch)
        {
            string urlKey = GetNormalizedUrlKey(ch);
            if (!string.IsNullOrEmpty(urlKey))
                return $"url:{urlKey}";

            string nameKey = NormalizeName(ch.name ?? "").ToLowerInvariant();
            if (!string.IsNullOrEmpty(nameKey))
                return $"name:{nameKey}";

            string tvgKey = (ch.tvg_id ?? "").Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(tvgKey))
                return $"tvg:{tvgKey}";

            string logoKey = (ch.logo_url ?? "").Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(logoKey))
                return $"logo:{logoKey}";

            return $"unknown:{Guid.NewGuid()}";
        }

        private static bool UrlsMatch(ChannelJson a, ChannelJson b)
        {
            if (a.stream_urls == null || b.stream_urls == null || a.stream_urls.Count == 0 || b.stream_urls.Count == 0)
                return false;

            var setA = a.stream_urls.Select(SimplifyUrl).Where(u => !string.IsNullOrEmpty(u)).Distinct().ToHashSet();
            var setB = b.stream_urls.Select(SimplifyUrl).Where(u => !string.IsNullOrEmpty(u)).Distinct().ToHashSet();

            return setA.Intersect(setB).Any();
        }
    }
}