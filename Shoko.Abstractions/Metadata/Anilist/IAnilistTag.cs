using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata.Anilist;

/// <summary>
/// An AniList tag.
/// </summary>
public interface IAnilistTag : ITag, IWithUpdateDate
{
    /// <summary>
    ///   The AniList category the tag belongs to.
    /// </summary>
    string Category { get; }

    /// <summary>
    ///   Indicates the tag is considered a tag for restricted content, for 
    ///   discrete eyes only. 🤫
    /// </summary>
    bool IsRestricted { get; }

    /// <summary>
    /// Indicates the tag is considered a spoiler for all anime it appears
    /// on.
    /// </summary>
    bool IsSpoiler { get; }

    /// <summary>
    /// All AniList anime the tag is set on.
    /// </summary>
    IReadOnlyList<IAnilistAnime> AllAnilistAnime { get; }
}

/// <summary>
/// An AniList tag with additional information for a single AniList anime.
/// </summary>
public interface IAnilistTagForAnime : IAnilistTag
{
    /// <summary>
    /// The ID of the AniList anime the tag is set on.
    /// </summary>
    int AnilistAnimeID { get; }

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
    /// A direct link to the AniList anime metadata.
    /// </summary>
    IAnilistAnime AnilistAnime { get; }
}
