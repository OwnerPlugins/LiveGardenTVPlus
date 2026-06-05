using System.Collections.Generic;

namespace LiveGardenTVPlus.Models
{
    public class ChannelJson
    {
        public string name { get; set; }
        public string country { get; set; }
        public List<string> youtube_urls { get; set; } = new List<string>();
        public List<string> stream_urls { get; set; } = new List<string>();
        public string nanoid { get; set; }
        public List<string> languages { get; set; } = new List<string>();
        public bool isGeoBlocked { get; set; }
        public string logo_url { get; set; }
        public string group { get; set; }
        public string tvg_id { get; set; }
        public bool isFavorite { get; set; }
        public string LanguagesDisplay => languages != null ? string.Join(", ", languages) : "";
        public string YoutubeUrlsDisplay => youtube_urls != null ? string.Join(", ", youtube_urls) : "";
        public string StreamUrlsDisplay => stream_urls != null ? string.Join(", ", stream_urls) : "";
        public string UrlStatus { get; set; } = "Unknown";
    }
}