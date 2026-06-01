namespace LiveGardenTV.Models
{
    public class EpgProgram
    {
        public string ChannelId { get; set; }
        public DateTime Start { get; set; }
        public DateTime Stop { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
    }
}