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

                string key = null;
                string value = null;

                // Support separator "::" (primary)
                int doubleColonIndex = trimmed.IndexOf("::");
                if (doubleColonIndex > 0)
                {
                    key = trimmed.Substring(0, doubleColonIndex).Trim();
                    value = trimmed.Substring(doubleColonIndex + 2).Trim();
                }
                else
                {
                    // Fallback: support ":" or "=" for backward compatibility
                    int sepIndex = trimmed.IndexOfAny(new char[] { ':', '=' });
                    if (sepIndex > 0)
                    {
                        key = trimmed.Substring(0, sepIndex).Trim();
                        value = trimmed.Substring(sepIndex + 1).Trim();
                    }
                }

                if (!string.IsNullOrEmpty(key))
                {
                    dict[key] = value ?? key;
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