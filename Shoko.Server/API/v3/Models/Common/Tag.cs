using System;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Models.Server;
using Shoko.Server.Repositories;

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

    public Tag(CustomTag tag, bool excludeDescription = false, int? size = null)
    {
        ID = tag.CustomTagID;
        Name = tag.TagName;
        if (!excludeDescription)
            Description = tag.TagDescription;
        Source = "User";
        IsSpoiler = false;
        Size = size;
    }

    public Tag(AniDB_Tag tag, bool excludeDescription = false, int? size = null)
    {
        ID = tag.TagID;
        ParentID = tag.ParentTagID;
        Name = tag.TagName;
        if (!excludeDescription)
            Description = tag.TagDescription;
        Source = "AniDB";
        IsVerified = tag.Verified;
        IsSpoiler = tag.GlobalSpoiler;
        Size = size;
        LastUpdated = tag.LastUpdated.ToUniversalTime();
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
    /// Number of series the tag appears on.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? Size { get; set; }

    /// <summary>
    /// When the tag info was last updated.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    [JsonConverter(typeof(IsoDateTimeConverter))]
    public DateTime? LastUpdated { get; set; }

    /// <summary>
    /// Source. Anidb, User, etc.
    /// </summary>
    /// <value></value>
    public string Source { get; set; }

    public static class Input
    {
        /// <summary>
        /// Create or update a custom tag.
        /// </summary>
        public class CreateOrUpdateCustomTagBody
        {
            /// <summary>
            /// Set the tag name. Set to null or empty to skip. Cannot be null
            /// or empty when creating a new tag.
            /// </summary>
            public string? Name { get; set; } = null;

            /// <summary>
            /// Set the tag description. Set to null to skip. Set to any string
            /// value to override existing or set the new description.
            /// </summary>
            public string? Description { get; set; } = null;

            public Tag? MergeWithExisting(CustomTag tag, ModelStateDictionary modelState)
            {
                if (!string.IsNullOrEmpty(Name?.Trim()))
                {
                    var existing = RepoFactory.CustomTag.GetByTagName(Name);
                    if (existing is not null && existing.CustomTagID != tag.CustomTagID)
                        modelState.AddModelError(nameof(Name), "Unable to create duplicate tag with the same name.");
                }

                if (!modelState.IsValid)
                    return null;

                var updated = tag.CustomTagID is 0;
                if (!string.IsNullOrEmpty(Name?.Trim()))
                {
                    tag.TagName = Name;
                    updated = true;
                }

                if (Description is not null)
                {
                    tag.TagDescription = Description.Trim();
                    updated = true;
                }

                if (updated)
                    RepoFactory.CustomTag.Save(tag);

                return new(tag);
            }
        }
    }
}
