using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LiveGardenTVPlus.Models
{
    public class Channel : INotifyPropertyChanged
    {
        private string _name;
        private string _url;
        private string _logo;
        private string _group;
        private string _tvgId;
        private bool _isFavorite;
        private string _urlStatus = "Unknown";
        private List<string> _youtubeUrls = new List<string>();
        private List<string> _streamUrls = new List<string>();

        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
        public string Url { get => _url; set { _url = value; OnPropertyChanged(); } }
        public string Logo { get => _logo; set { _logo = value; OnPropertyChanged(); } }
        public string Group { get => _group; set { _group = value; OnPropertyChanged(); } }
        public string TvgId { get => _tvgId; set { _tvgId = value; OnPropertyChanged(); } }
        public List<string> YoutubeUrls { get => _youtubeUrls; set { _youtubeUrls = value; OnPropertyChanged(); } }
        public List<string> StreamUrls { get => _streamUrls; set { _streamUrls = value; OnPropertyChanged(); } }
        public event PropertyChangedEventHandler PropertyChanged;
        public bool IsFavorite { get => _isFavorite; set { _isFavorite = value; OnPropertyChanged(); } }
        public string UrlStatus { get => _urlStatus; set { _urlStatus = value; OnPropertyChanged(); } }
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private string _logoUrl;
        public string logo_url
        {
            get => _logoUrl;
            set => _logoUrl = string.IsNullOrEmpty(value) ? null : value;
        }
    }
}
