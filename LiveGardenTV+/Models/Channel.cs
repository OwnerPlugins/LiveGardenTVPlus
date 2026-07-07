using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LiveGardenTVPlus.Models
{
    public class Channel : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _url = string.Empty;
        private string _logo = string.Empty;
        private string _group = string.Empty;
        private string _tvgId = string.Empty;
        private bool _isFavorite;
        private string _urlStatus = "Unknown";
        private List<string> _youtubeUrls = new List<string>();
        private List<string> _streamUrls = new List<string>();
        private bool _isRadio;
        private string? _logoUrl; // nullable, because it can be null

        public bool IsRadio
        {
            get => _isRadio;
            set { _isRadio = value; OnPropertyChanged(); }
        }

        public string Name { get => _name; set { _name = value ?? string.Empty; OnPropertyChanged(); } }
        public string Url { get => _url; set { _url = value ?? string.Empty; OnPropertyChanged(); } }
        public string Logo { get => _logo; set { _logo = value ?? string.Empty; OnPropertyChanged(); } }
        public string Group { get => _group; set { _group = value ?? string.Empty; OnPropertyChanged(); } }
        public string TvgId { get => _tvgId; set { _tvgId = value ?? string.Empty; OnPropertyChanged(); } }

        public List<string> YoutubeUrls
        {
            get => _youtubeUrls;
            set { _youtubeUrls = value ?? new List<string>(); OnPropertyChanged(); }
        }

        public List<string> StreamUrls
        {
            get => _streamUrls;
            set { _streamUrls = value ?? new List<string>(); OnPropertyChanged(); }
        }

        public bool IsFavorite
        {
            get => _isFavorite;
            set { _isFavorite = value; OnPropertyChanged(); }
        }

        public string UrlStatus
        {
            get => _urlStatus;
            set { _urlStatus = value ?? "Unknown"; OnPropertyChanged(); }
        }

        public string? logo_url
        {
            get => _logoUrl;
            set => _logoUrl = string.IsNullOrEmpty(value) ? null : value;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name ?? string.Empty));
    }
}
