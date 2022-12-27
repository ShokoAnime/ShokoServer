using System;
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
        IsSpoiler = false;
    }

    public Tag(AniDB_Tag tag, bool excludeDescriptions = false)
    {
        ID = tag.TagID;
        ParentID = tag.ParentTagID;
        Name = tag.TagName;
        if (!excludeDescriptions)
            Description = tag.TagDescription;
        Source = "AniDB";
        IsVerified = tag.Verified;
        IsSpoiler = tag.GlobalSpoiler;
        LastUpdated = tag.LastUpdated;
    }

    /// <summary>
    /// Tag id. Relative to it's source for now.
    /// </summary>
    /// <value></value>
    public int ID { get; set; }

    /// <summary>
    /// The parent tag id, if any. Relative to it's source for now.
    /// </summary>
    public int? ParentID { get; set; }

    /// <summary>
    /// The tag itself.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// What does the tag mean/what's it for.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }

    /// <summary>
    /// True if the tag has been verified.
    /// </summary>
    /// <remarks>
    /// For anidb does this mean the tag has been verified for use, and is not
    /// an unsorted tag. Also, anidb hides unverified tags from appearing in
    /// their UI except when the tags are edited.
    /// </remarks>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsVerified { get; set; }

    /// <summary>
    /// True if the tag is considered a spoiler for all series it appears on.
    /// </summary>
    public bool IsSpoiler { get; set; }

    /// <summary>
    /// True if the tag is considered a spoiler for that particular series it is
    /// set on.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsLocalSpoiler { get; set; }

    /// <summary>
    /// How relevant is it to the series
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? Weight { get; set; }

    /// <summary>
    /// When the tag info was last updated.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public DateTime? LastUpdated { get; set; }
    
    /// <summary>
    /// Source. Anidb, User, etc.
    /// </summary>
    /// <value></value>
    public string Source { get; set; }
}
