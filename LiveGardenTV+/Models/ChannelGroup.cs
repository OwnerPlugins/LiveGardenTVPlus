using System.Collections.ObjectModel;

namespace LiveGardenTVPlus.Models
{
    public class ChannelGroup
    {
        public string? GroupName { get; set; }
        public ObservableCollection<Channel> Channels { get; set; } = new ObservableCollection<Channel>();
    }
}