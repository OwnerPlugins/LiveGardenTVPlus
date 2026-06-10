using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json;

namespace LiveGardenTVPlus.Models
{
    public class ChannelJson : INotifyPropertyChanged
    {
        private string _name;
        private List<string> _streamUrls;
        private string _logoUrl;
        private string _group;
        private string _tvgId;
        private bool _isFavorite;
        private string _country;
        private List<string> _youtubeUrls;
        private string _nanoid;
        private List<string> _languages;
        private bool _isGeoBlocked;
        private string _urlStatus;

        [JsonProperty("name")]
        public string name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(name)); }
        }

        [JsonProperty("stream_urls")]
        public List<string> stream_urls
        {
            get => _streamUrls ??= new List<string>();
            set 
            { 
                _streamUrls = value; 
                OnPropertyChanged(nameof(stream_urls)); 
                OnPropertyChanged(nameof(StreamUrlsDisplay));
                OnPropertyChanged(nameof(PrimaryUrl));
            }
        }

        [JsonProperty("logo_url")]
        public string logo_url
        {
            get => _logoUrl;
            set { _logoUrl = value; OnPropertyChanged(nameof(logo_url)); }
        }

        [JsonProperty("group")]
        public string group
        {
            get => _group;
            set { _group = value ?? ""; OnPropertyChanged(nameof(group)); }
        }

        [JsonProperty("tvg_id")]
        public string tvg_id
        {
            get => _tvgId;
            set { _tvgId = value; OnPropertyChanged(nameof(tvg_id)); }
        }

        [JsonProperty("isFavorite")]
        public bool isFavorite
        {
            get => _isFavorite;
            set { _isFavorite = value; OnPropertyChanged(nameof(isFavorite)); }
        }

        [JsonProperty("country")]
        public string country
        {
            get => _country;
            set { _country = value; OnPropertyChanged(nameof(country)); }
        }

        [JsonProperty("youtube_urls")]
        public List<string> youtube_urls
        {
            get => _youtubeUrls ??= new List<string>();
            set { _youtubeUrls = value; OnPropertyChanged(nameof(youtube_urls)); OnPropertyChanged(nameof(YoutubeUrlsDisplay)); }
        }

        [JsonProperty("nanoid")]
        public string nanoid
        {
            get => _nanoid;
            set { _nanoid = value; OnPropertyChanged(nameof(nanoid)); }
        }

        [JsonProperty("languages")]
        public List<string> languages
        {
            get => _languages ??= new List<string>();
            set { _languages = value; OnPropertyChanged(nameof(languages)); OnPropertyChanged(nameof(LanguagesDisplay)); }
        }

        [JsonProperty("isGeoBlocked")]
        public bool isGeoBlocked
        {
            get => _isGeoBlocked;
            set { _isGeoBlocked = value; OnPropertyChanged(nameof(isGeoBlocked)); }
        }

        [JsonIgnore]
        public string LanguagesDisplay => languages != null ? string.Join(", ", languages) : "";

        [JsonIgnore]
        public string YoutubeUrlsDisplay => youtube_urls != null ? string.Join(", ", youtube_urls) : "";

        [JsonIgnore]
        public string StreamUrlsDisplay => stream_urls != null ? string.Join(", ", stream_urls) : "";

        [JsonIgnore]
        public string UrlStatus
        {
            get => _urlStatus;
            set { _urlStatus = value; OnPropertyChanged(nameof(UrlStatus)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        [JsonIgnore]
        public string PrimaryUrl
        {
            get => stream_urls != null && stream_urls.Count > 0 ? stream_urls[0] : "";
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    stream_urls = new List<string>();
                }
                else if (value.Contains(','))
                {
                    var urls = value.Split(',')
                                    .Select(u => u.Trim())
                                    .Where(u => !string.IsNullOrEmpty(u))
                                    .Distinct()  // evita duplicati nella stessa cella
                                    .ToList();
                    stream_urls = urls;
                }
                else
                {
                    stream_urls = new List<string> { value };
                }
                OnPropertyChanged(nameof(PrimaryUrl));
                OnPropertyChanged(nameof(StreamUrlsDisplay));
            }
        }
    }
}


