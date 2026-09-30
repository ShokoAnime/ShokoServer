using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata.Anidb;

/// <summary>
/// An AniDB tag.
/// </summary>
public interface IAnidbTag : ITag, IWithUpdateDate
{
    /// <summary>
    ///   The AniDB tag ID, the same ID <see cref="IMetadata.ID"/> holds as
    ///   text.
    /// </summary>
    int AnidbID { get; }

    /// <summary>
    ///   The parent AniDB tag, or <c>null</c> for a tag at the top of the tree.
    /// </summary>
    MetadataGuid? ParentTagID { get; }

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
    new bool IsSpoiler { get; }

    bool ITag.IsSpoiler { get => IsSpoiler; }

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
    new int Weight { get; }

    int? ITag.Weight { get => Weight; }

    /// <summary>
    /// Indicates the tag is considered a spoiler for that particular anime
    /// it is set on.
    /// </summary>
    bool IsLocalSpoiler { get; }

    bool ITag.IsSpoiler { get => IsSpoiler || IsLocalSpoiler; }

    /// <summary>
    /// A direct link to the AniDB anime metadata.
    /// </summary>
    IAnidbAnime AnidbAnime { get; }
}
