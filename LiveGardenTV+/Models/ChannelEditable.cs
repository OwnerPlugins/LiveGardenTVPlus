namespace LiveGardenTVPlus.Models
{
    /// <summary>
    /// Rappresenta una versione modificabile di <see cref="Channel"/> con proprietà
    /// aggiuntive usate dall'interfaccia di editing (es. StreamUrls, YoutubeUrls, Country).
    /// </summary>
    public class ChannelEditable : Channel
    {
        public new List<string> StreamUrls { get; set; } = new List<string>();
        public new List<string> YoutubeUrls { get; set; } = new List<string>();
        public string Country { get; set; } = "";
        public List<string> Languages { get; set; } = new List<string>();
        public string Nanoid { get; set; } = "";
        public bool IsGeoBlocked { get; set; } = false;
    }
}
