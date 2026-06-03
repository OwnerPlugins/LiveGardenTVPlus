using Newtonsoft.Json;
using System.IO;

namespace LiveGardenTVPlus.Services
{
    public class UserPreferences
    {
        public string Language { get; set; } = "English";
        public string PlaylistUrl { get; set; } = "";
        public string EpgUrl { get; set; }
        public int BufferSeconds { get; set; } = 3;
        public string Theme { get; set; } = "LightTheme";
        public bool SortAlphabetically { get; set; } = false;
        private static string PrefsFile => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "userprefs.json");
        public void Save() => File.WriteAllText(PrefsFile, JsonConvert.SerializeObject(this, Formatting.Indented));
        public static UserPreferences Load() => File.Exists(PrefsFile) ? JsonConvert.DeserializeObject<UserPreferences>(File.ReadAllText(PrefsFile)) ?? new UserPreferences() : new UserPreferences();
    }
}