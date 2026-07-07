using System.Collections.ObjectModel;

namespace LiveGardenTVPlus.Models
{
#pragma warning disable CS1591 // Manca il commento XML per il tipo o il membro visibile pubblicamente
    public class ChannelGroup
#pragma warning restore CS1591 // Manca il commento XML per il tipo o il membro visibile pubblicamente
    {
        public string? GroupName { get; set; }
        public ObservableCollection<Channel> Channels { get; set; } = new ObservableCollection<Channel>();
    }
}