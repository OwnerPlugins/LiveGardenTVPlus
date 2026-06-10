using System.Collections.Generic;

namespace LiveGardenTVPlus.Models
{
    /// <summary>
    /// Represents a single mapping from a JSON property to a ChannelJson field.
    /// </summary>
    public class MappingConfig
    {
        public string SourcePropertyName { get; set; }
        public string TargetField { get; set; }
    }

    /// <summary>
    /// Saved mapping profile for a specific JSON file.
    /// </summary>
    public class SavedMapping
    {
        public string FileNamePattern { get; set; }
        public List<MappingConfig> Mappings { get; set; }
    }
}