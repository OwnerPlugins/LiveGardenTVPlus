/* example from: https://raw.githubusercontent.com/SHAJON-404/iptv/refs/heads/main/app/data/channels.json */
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using LiveGardenTVPlus.Models;
using LiveGardenTVPlus.Services;
using Newtonsoft.Json.Linq;

namespace LiveGardenTVPlus.Views
{
    public partial class JsonImportMappingWindow : Window
    {
        private string _jsonText;
        private string _fileName;
        private JArray _jsonArray;
        private List<string> _availableProperties;
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
                // Parse JSON: Supports both direct array and object with array properties
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

                _availableProperties = ExtractAllProperties(_jsonArray);
                System.Diagnostics.Debug.WriteLine($"Properties found: {_availableProperties.Count}");
                foreach (var p in _availableProperties)
                    System.Diagnostics.Debug.WriteLine($" - {p}");

                // Load saved mapping for this file if exists
                var prefs = UserPreferences.Load();
                string key = System.IO.Path.GetFileName(fileName);
                if (prefs.JsonMappings.ContainsKey(key))
                {
                    var saved = Newtonsoft.Json.JsonConvert.DeserializeObject<List<MappingConfig>>(prefs.JsonMappings[key]);
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

        private List<string> ExtractAllProperties(JArray array)
        {
            var props = new HashSet<string>();
            foreach (var item in array.Children<JObject>())
            {
                foreach (var prop in item.Properties())
                    props.Add(prop.Name);
            }
            return props.OrderBy(p => p).ToList();
        }

        private void AutoDetectBtn_Click(object sender, RoutedEventArgs e)
        {
            Mappings.Clear();
            foreach (var prop in _availableProperties)
            {
                string lower = prop.ToLower();
                string target = null;

                if (lower == "name" || lower == "title" || lower == "tvg_name" || lower == "channel_name")
                    target = "name";
                else if (lower == "url" || lower == "stream_url" || lower == "link" || lower == "source")
                    target = "stream_urls";
                else if (lower == "logo" || lower == "logo_url" || lower == "tvg_logo")
                    target = "logo_url";
                else if (lower == "group" || lower == "category" || lower == "group_title" || lower == "group-title")
                    target = "group";
                else if (lower == "tvg_id" || lower == "epg_id" || lower == "channel_id")
                    target = "tvg_id";
                else if (lower == "favorite" || lower == "is_favorite" || lower == "fav")
                    target = "isFavorite";
                else if (lower == "country" || lower == "tvg_country")
                    target = "country";
                else if (lower == "language" || lower == "languages" || lower == "audio_lang")
                    target = "languages";
                else if (lower == "youtube" || lower == "youtube_url")
                    target = "youtube_urls";
                else if (lower == "nanoid")
                    target = "nanoid";
                else if (lower == "geoblocked" || lower == "geo_blocked" || lower == "is_geoblocked")
                    target = "isGeoBlocked";

                if (target != null)
                    Mappings.Add(new MappingConfig { SourcePropertyName = prop, TargetField = target });
            }

            // If no mapping was found for "name", add a blank line
            if (!Mappings.Any(m => m.TargetField == "name"))
                Mappings.Add(new MappingConfig { SourcePropertyName = "", TargetField = "name" });

            UpdatePreview();
        }

        private void AddMappingBtn_Click(object sender, RoutedEventArgs e)
        {
            Mappings.Add(new MappingConfig { SourcePropertyName = "", TargetField = "" });
        }

        private void SaveMappingBtn_Click(object sender, RoutedEventArgs e)
        {
            var prefs = UserPreferences.Load();
            string key = System.IO.Path.GetFileName(_fileName);
            string serialized = Newtonsoft.Json.JsonConvert.SerializeObject(Mappings.ToList());
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