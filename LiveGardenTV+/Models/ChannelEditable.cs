using System.Collections.Generic;

namespace LiveGardenTVPlus.Models
{
    public class ChannelEditable : Channel
    {
        public string Country { get; set; } = "";
        public List<string> YoutubeUrls { get; set; } = new List<string>();
        public List<string> StreamUrls { get; set; } = new List<string>();  // multiple URLs, but we use Url for main
        public string Nanoid { get; set; } = "";
        public List<string> Languages { get; set; } = new List<string>();
        public bool IsGeoBlocked { get; set; } = false;
    }
}