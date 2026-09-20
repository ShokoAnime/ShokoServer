namespace Shoko.Abstractions.Metadata.Anidb;

/// <summary>
/// An anime AniDB's users find similar to another one. AniDB votes on its
/// suggestions, so this adds the raw vote counts behind
/// <see cref="ISuggestedMetadata.ApprovalRating"/>.
/// </summary>
public interface IAnidbSuggestion : ISuggestedMetadata<IAnidbAnime, IAnidbAnime>
{
    /// <summary>
    ///   The number of votes in favor of the similarity match. Used to
    ///   calculate the <see cref="ISuggestedMetadata.ApprovalRating" />.
    /// </summary>
    int ApprovalVotes { get; }

    /// <summary>
    ///   The total number of votes on the similarity match. Used to calculate
    ///   the <see cref="ISuggestedMetadata.ApprovalRating" />.
    /// </summary>
    int TotalVotes { get; }
}
