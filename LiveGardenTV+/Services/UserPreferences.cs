using Newtonsoft.Json;
using System.IO;

namespace LiveGardenTVPlus.Services
{
    public class UserPreferences
    {
        public string Language { get; set; } = "English";
        public string Theme { get; set; } = "LightTheme";
        public static UserPreferences Load() => File.Exists(PrefsFile) ? JsonConvert.DeserializeObject<UserPreferences>(File.ReadAllText(PrefsFile)) ?? new UserPreferences() : new UserPreferences();
        public string LogosRepositoryOwner { get; set; } = "OwnerPlugins";
        public string LogosRepositoryRepo { get; set; } = "logos";
        public string LogosRepositoryPath { get; set; } = "logos/SNP";
        public bool LogosEnabled { get; set; } = true;
        public string LogosSubFolder { get; set; } = "SNP";   // "SNP", "PROVIDER", "ALL"
        public string LogosListUrl { get; set; } = "https://raw.githubusercontent.com/OwnerPlugins/logos/main/txt/logos.txt";
        public string PlaylistUrl { get; set; } = "";
        public string EpgUrl { get; set; }
        public List<string> RecentPlaylists { get; set; } = new List<string>();
        public const int MaxRecentPlaylists = 5;
        public double LogoMatchingThreshold { get; set; } = 0.75;
        public double EpgMatchingThreshold { get; set; } = 0.75;
        public int BufferSeconds { get; set; } = 3;
        public bool SortAlphabetically { get; set; } = false;
        private static string PrefsFile => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "userprefs.json");
        public void Save() => File.WriteAllText(PrefsFile, JsonConvert.SerializeObject(this, Formatting.Indented));
        public Dictionary<string, string> JsonMappings { get; set; } = new Dictionary<string, string>();
    }
}