using System.IO;

namespace LiveGardenTVPlus.Services
{
    public static class LanguageManager
    {
        public static event Action LanguageChanged;
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

            var lines = File.ReadAllLines(path);
            var dict = new Dictionary<string, string>();
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                int sepIndex = trimmed.IndexOfAny(new char[] { ':', '=' });
                if (sepIndex > 0)
                {
                    string key = trimmed.Substring(0, sepIndex).Trim();
                    string value = trimmed.Substring(sepIndex + 1).Trim();
                    dict[key] = value;
                }
            }
            _dict = dict;
            TranslationHelper.ResetCache();
            LanguageChanged?.Invoke();
        }

        public static string GetTranslation(string key)
        {
            return _dict.TryGetValue(key, out string val) ? val : key;
        }
    }
}