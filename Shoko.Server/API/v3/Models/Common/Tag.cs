using Newtonsoft.Json;
using Shoko.Models.Server;

#nullable enable
namespace Shoko.Server.API.v3.Models.Common;

public class Tag
{
    public Tag()
    {
        ID = 0;
        Name = "";
        Source = "Shoko";
    }

    public Tag(CustomTag tag, bool excludeDescriptions = false)
    {
        ID = tag.CustomTagID;
        Name = tag.TagName;
        if (!excludeDescriptions)
            Description = tag.TagDescription;
        Source = "User";
    }

    public Tag(AniDB_Tag tag, bool excludeDescriptions = false)
    {
        ID = tag.TagID;
        Name = tag.TagName;
        if (!excludeDescriptions)
            Description = tag.TagDescription;
        Source = "AniDB";
    }

    /// <summary>
    /// Tag id. Relative to it's source for now.
    /// </summary>
    /// <value></value>
    public int ID { get; set; }

    /// <summary>
    /// The tag itself
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// What does the tag mean/what's it for
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }

    /// <summary>
    /// How relevant is it to the series
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? Weight { get; set; }
    
    /// <summary>
    /// Source. Anidb, User, etc.
    /// </summary>
    /// <value></value>
    public string Source { get; set; }
}
