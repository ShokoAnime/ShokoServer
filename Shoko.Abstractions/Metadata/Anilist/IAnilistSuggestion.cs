namespace Shoko.Abstractions.Metadata.Anilist;

/// <summary>
/// An anime AniList's users recommend to someone who liked another one.
/// AniList scores its recommendations rather than counting the voters, so
/// <see cref="ISuggestedMetadata.Votes"/> is always <see langword="null"/> and
/// <see cref="Rating"/> is what ranks them.
/// </summary>
public interface IAnilistSuggestion : ISuggestedMetadata<IAnilistAnime, IAnilistAnime>
{
    /// <summary>
    ///   AniList's net score for the recommendation: the votes for it minus
    ///   the votes against, so it can be negative.
    /// </summary>
    int Rating { get; }
}
