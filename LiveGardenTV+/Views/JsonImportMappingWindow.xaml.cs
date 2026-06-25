using LiveGardenTVPlus.Models;
using LiveGardenTVPlus.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace LiveGardenTVPlus.Views
{
    public partial class JsonImportMappingWindow : Window
    {
        private string _jsonText;
        private string _fileName;
        private JArray _jsonArray;
        private List<string> _availablePaths;
        private List<ChannelJson> _mappedChannels;

        public ObservableCollection<MappingConfig> Mappings { get; set; }

        public JsonImportMappingWindow(string jsonText, string fileName)
        {
            InitializeComponent();
            _jsonText = jsonText;
            _fileName = fileName;
            DataContext = this;
            Mappings = new ObservableCollection<MappingConfig>();

            try
            {
                var token = JToken.Parse(jsonText);
                if (token is JArray array)
                    _jsonArray = array;
                else if (token is JObject obj)
                {
                    var firstArray = obj.Properties().FirstOrDefault(p => p.Value is JArray);
                    if (firstArray != null)
                        _jsonArray = (JArray)firstArray.Value;
                    else
                        throw new Exception("JSON must contain an array of channels.");
                }
                else
                    throw new Exception("Invalid JSON format.");

                _availablePaths = ExtractAllPaths(_jsonArray);
                System.Diagnostics.Debug.WriteLine($"Properties found: {_availablePaths.Count}");
                foreach (var p in _availablePaths)
                    System.Diagnostics.Debug.WriteLine($" - {p}");

                var prefs = UserPreferences.Load();
                string key = Path.GetFileName(fileName);
                if (prefs.JsonMappings.ContainsKey(key))
                {
                    var saved = JsonConvert.DeserializeObject<List<MappingConfig>>(prefs.JsonMappings[key]);
                    if (saved != null)
                        foreach (var m in saved) Mappings.Add(m);
                }

                MappingsGrid.ItemsSource = Mappings;
                ApplyLanguage();
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LanguageManager.GetTranslation("Invalid JSON: {0}"), ex.Message),
                                LanguageManager.GetTranslation("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                DialogResult = false;
                Close();
                return;
            }

            LanguageManager.LanguageChanged += ApplyLanguage;
        }

        private void ApplyLanguage()
        {
            Title = LanguageManager.GetTranslation("Import JSON");
            InstructionText.Text = LanguageManager.GetTranslation("Map JSON properties to channel fields:");
            PreviewHeaderText.Text = LanguageManager.GetTranslation("Preview (first 5 channels)");
            AutoDetectBtn.Content = LanguageManager.GetTranslation("Auto-detect");
            SaveMappingBtn.Content = LanguageManager.GetTranslation("Save Mapping");
            ImportBtn.Content = LanguageManager.GetTranslation("Import");
            CancelBtn.Content = LanguageManager.GetTranslation("Cancel");
            AddMappingBtn.Content = LanguageManager.GetTranslation("Add Mapping");

            if (MappingsGrid.Columns.Count >= 2)
            {
                MappingsGrid.Columns[0].Header = LanguageManager.GetTranslation("JSON Property");
                MappingsGrid.Columns[1].Header = LanguageManager.GetTranslation("Target Field");
            }
        }

        private List<string> ExtractAllPaths(JArray array)
        {
            var paths = new HashSet<string>();
            foreach (var child in array.Children())
            {
                if (child is JObject obj)
                    CollectPaths(obj, "", paths);
            }
            return paths.OrderBy(p => p).ToList();
        }

        private void CollectPaths(JToken token, string prefix, HashSet<string> paths)
        {
            if (token is JObject obj)
            {
                foreach (var prop in obj.Properties())
                {
                    string path = string.IsNullOrEmpty(prefix) ? prop.Name : prefix + "." + prop.Name;
                    paths.Add(path);
                    if (prop.Value is JObject || prop.Value is JArray)
                        CollectPaths(prop.Value, path, paths);
                }
            }
            // If token is not an object (e.g., JArray, JValue), do nothing
        }

        private void AutoDetectBtn_Click(object sender, RoutedEventArgs e)
        {
            Mappings.Clear();
            var scoredPaths = new List<(string Path, int Score, string Target)>();

            foreach (var path in _availablePaths)
            {
                int score = 0;
                string lower = path.ToLower();
                string target = null;

                if (lower == "name" || lower.EndsWith(".name") || lower == "title" || lower.EndsWith(".title"))
                { score += 10; target = "name"; }
                else if (lower.Contains("name") || lower.Contains("title") || lower.Contains("channel_name"))
                { score += 5; target = "name"; }

                if (lower == "url" || lower.EndsWith(".url") || lower == "stream_url" || lower == "link" || lower.EndsWith(".stream_url"))
                { score += 10; target = "stream_urls"; }
                else if (lower.Contains("url") || lower.Contains("stream") || lower.Contains("link"))
                { score += 5; target = "stream_urls"; }

                if (lower == "logo" || lower.EndsWith(".logo") || lower == "logo_url" || lower.EndsWith(".logo_url") || lower == "tvg_logo")
                { score += 10; target = "logo_url"; }
                else if (lower.Contains("logo") || lower.Contains("icon") || lower.Contains("image"))
                { score += 5; target = "logo_url"; }

                if (lower == "group" || lower.EndsWith(".group") || lower == "group_title" || lower == "category")
                { score += 10; target = "group"; }
                else if (lower.Contains("group") || lower.Contains("category") || lower.Contains("genre"))
                { score += 5; target = "group"; }

                if (lower == "tvg_id" || lower.EndsWith(".id") || lower == "channel_id" || lower == "epg_id")
                { score += 10; target = "tvg_id"; }
                else if (lower.Contains("tvg") || lower.Contains("epg") || lower.Contains("id"))
                { score += 5; target = "tvg_id"; }

                if (lower == "country" || lower.EndsWith(".country") || lower == "tvg_country")
                { score += 10; target = "country"; }
                else if (lower.Contains("country"))
                { score += 5; target = "country"; }

                if (lower == "language" || lower == "languages" || lower.EndsWith(".language") || lower.EndsWith(".languages"))
                { score += 10; target = "languages"; }

                if (lower == "favorite" || lower == "is_favorite" || lower.EndsWith(".favorite") || lower.EndsWith(".is_favorite"))
                { score += 10; target = "isFavorite"; }

                if (lower == "geoblocked" || lower == "geo_blocked" || lower == "is_geoblocked" || lower.EndsWith(".geoblocked"))
                { score += 10; target = "isGeoBlocked"; }

                if (lower == "nanoid" || lower.EndsWith(".nanoid"))
                { score += 10; target = "nanoid"; }

                if (lower.Contains("youtube") || lower.Contains("yt"))
                { score += 10; target = "youtube_urls"; }

                if (target != null && score > 0)
                    scoredPaths.Add((path, score, target));
            }

            var bestMappings = scoredPaths
                .GroupBy(x => x.Target)
                .Select(g => g.OrderByDescending(x => x.Score).First())
                .ToList();

            foreach (var m in bestMappings)
                Mappings.Add(new MappingConfig { SourcePropertyName = m.Path, TargetField = m.Target });

            if (!Mappings.Any(m => m.TargetField == "name"))
                Mappings.Add(new MappingConfig { SourcePropertyName = "", TargetField = "name" });
            if (!Mappings.Any(m => m.TargetField == "stream_urls"))
                Mappings.Add(new MappingConfig { SourcePropertyName = "", TargetField = "stream_urls" });

            UpdatePreview();
        }

        private void AddMappingBtn_Click(object sender, RoutedEventArgs e)
        {
            Mappings.Add(new MappingConfig { SourcePropertyName = "", TargetField = "" });
        }

        private void SaveMappingBtn_Click(object sender, RoutedEventArgs e)
        {
            var prefs = UserPreferences.Load();
            string key = Path.GetFileName(_fileName);
            string serialized = JsonConvert.SerializeObject(Mappings.ToList());
            prefs.JsonMappings[key] = serialized;
            prefs.Save();
            MessageBox.Show(LanguageManager.GetTranslation("Mapping saved for future imports of this file."),
                            LanguageManager.GetTranslation("Saved"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UpdatePreview()
        {
            try
            {
                _mappedChannels = JsonMapper.MapFromJson(_jsonText, Mappings.ToList());
                var first5 = _mappedChannels.Take(5).Select(c => $"{c.name} ({c.group})").ToList();
                PreviewListBox.ItemsSource = first5;
                if (first5.Count == 0)
                    PreviewListBox.ItemsSource = new List<string> { LanguageManager.GetTranslation("No channels mapped") };
            }
            catch (Exception ex)
            {
                PreviewListBox.ItemsSource = new List<string> { string.Format(LanguageManager.GetTranslation("Preview error: {0}"), ex.Message) };
            }
        }

        private void ImportBtn_Click(object sender, RoutedEventArgs e)
        {
            UpdatePreview();
            if (_mappedChannels == null || _mappedChannels.Count == 0)
            {
                MessageBox.Show(LanguageManager.GetTranslation("No channels could be mapped. Check your mapping rules."),
                                LanguageManager.GetTranslation("Warning"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
            Close();
        }

        public List<ChannelJson> GetMappedChannels() => _mappedChannels;
    }
}