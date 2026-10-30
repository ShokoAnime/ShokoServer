namespace Shoko.Abstractions.Metadata.Anidb;

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
