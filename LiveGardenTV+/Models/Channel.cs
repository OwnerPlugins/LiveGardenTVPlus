using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LiveGardenTVPlus.Models
{
    public class Channel : INotifyPropertyChanged
    {
        private bool _isFavorite;
        private string _name;
        private string _url;
        private string _logo;
        private string _group;
        private string _tvgId;

        private string _urlStatus = "Unknown";
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
        public string Url { get => _url; set { _url = value; OnPropertyChanged(); } }
        public string Logo { get => _logo; set { _logo = value; OnPropertyChanged(); } }
        public string Group { get => _group; set { _group = value; OnPropertyChanged(); } }
        public string TvgId { get => _tvgId; set { _tvgId = value; OnPropertyChanged(); } }
        public event PropertyChangedEventHandler PropertyChanged;
        
         public bool IsFavorite

        {
            get => _isFavorite;
            set
            {
                if (_isFavorite != value)
                {
                    _isFavorite = value;
                    OnPropertyChanged();
                }
            }
        }

        public string UrlStatus
        {
            get => _urlStatus;
            set { _urlStatus = value; OnPropertyChanged(); }
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/* namespace LiveGardenTVPlus.Models
{
    public class Channel
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public string Logo { get; set; }
        public string Group { get; set; }
        public string TvgId { get; set; }
        public bool IsFavorite { get; set; }
    }
}
 */
