namespace LiveGardenTVPlus.Models
{
    public class EpgChannel
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Icon { get; set; }
        public List<EpgProgramme> Programmes { get; set; } = new List<EpgProgramme>();
    }

    public class EpgProgramme
    {
        public DateTime Start { get; set; }   // UTC
        public DateTime Stop { get; set; }    // UTC
        public string Title { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
    }
}