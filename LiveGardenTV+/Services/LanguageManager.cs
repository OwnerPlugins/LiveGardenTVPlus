using System.IO;

namespace LiveGardenTVPlus.Services
{
    public static class LanguageManager
    {
        private static Dictionary<string, string> _dict = new Dictionary<string, string>();

        public static List<string> GetAvailableLanguages()
        {
            string langDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Languages");
            if (!Directory.Exists(langDir))
                return new List<string> { "English" };

            return Directory.GetFiles(langDir, "*.lng")
                            .Select(Path.GetFileNameWithoutExtension)
                            .ToList();
        }

        public static void LoadLanguage(string languageName)
        {
            string langDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Languages");
            if (!Directory.Exists(langDir))
                langDir = Path.Combine(Directory.GetCurrentDirectory(), "Languages");

            string path = Path.Combine(langDir, $"{languageName}.lng");
            if (!File.Exists(path))
            {
                path = Path.Combine(langDir, "English.lng");
                if (!File.Exists(path)) return;
            }
            _dict = File.ReadAllLines(path)
                .Where(l => !string.IsNullOrWhiteSpace(l) && l.Contains('='))
                .Select(l => l.Split(new[] { '=' }, 2))
                .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());
        }

        public static string GetTranslation(string key) => _dict.TryGetValue(key, out string val) ? val : key;
    }
}