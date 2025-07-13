
using System;
using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.DataModels.Anidb;

/// <summary>
/// An AniDB tag.
/// </summary>
public interface IAnidbTag : IMetadata<int>
{
    /// <summary>
    /// The parent tag ID, if any.
    /// </summary>
    int? ParentTagID { get; }

    /// <summary>
    /// The tag name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// What does the tag mean/what's it for.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Indicates the tag has been verified.
    /// </summary>
    /// <remarks>
    /// This means the tag has been verified for use, and is not an unsorted
    /// tag. Also, AniDB hides unverified tags from appearing in their UI
    /// except when the tags are edited.
    /// </remarks>
    bool IsVerified { get; }

    /// <summary>
    /// Indicates the tag is considered a spoiler for all anime it appears
    /// on.
    /// </summary>
    bool IsSpoiler { get; }

    /// <summary>
    /// When the tag info was last updated.
    /// </summary>
    DateTime LastUpdated { get; }

    /// <summary>
    /// The parent tag, if any.
    /// </summary>
    IAnidbTag? ParentTag { get; }

    /// <summary>
    /// The child tags, if any.
    /// </summary>
    IReadOnlyList<IAnidbTag> ChildTags { get; }

    /// <summary>
    /// All AniDB anime the tag is set on.
    /// </summary>
    IReadOnlyList<IAnidbAnime> AllAnidbAnime { get; }
}

/// <summary>
/// An AniDB tag with additional information for a single AniDB anime.
/// </summary>
public interface IAnidbTagForAnime : IAnidbTag
{
    /// <summary>
    /// The ID of the AniDB anime the tag is set on.
    /// </summary>
    int AnidbAnimeID { get; }

    /// <summary>
    /// How relevant is the tag is to the anime, or if it's weightless.
    /// </summary>
    int Weight { get; }

    /// <summary>
    /// Indicates the tag is considered a spoiler for that particular anime
    /// it is set on.
    /// </summary>
    bool IsLocalSpoiler { get; }

    /// <summary>
    /// A direct link to the anidb anime metadata.
    /// </summary>
    IAnidbAnime AnidbAnime { get; }
}
