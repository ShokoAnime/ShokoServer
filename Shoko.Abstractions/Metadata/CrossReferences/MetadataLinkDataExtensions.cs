using System;
using Shoko.Abstractions.Metadata.Matching;

namespace Shoko.Abstractions.Metadata.CrossReferences;

/// <summary>
///   Turns a link you have into the shape the store writes.
/// </summary>
/// <remarks>
///   A cross-reference describes a link that exists and a
///   <see cref="MetadataLinkData" /> one that is about to, so going from the
///   first to the second drops what only a stored link has: where it sits, and
///   who wrote it.
/// </remarks>
public static class MetadataLinkDataExtensions
{
    /// <summary>
    ///   Reads a series link back into the shape it would be written as.
    /// </summary>
    /// <param name="crossReference">The link as it reads.</param>
    /// <returns>The same link, ready to write.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="crossReference" /> is <c>null</c>.
    /// </exception>
    public static MetadataSeriesLinkData ToLinkData(this IMetadataSeriesCrossReference crossReference)
    {
        ArgumentNullException.ThrowIfNull(crossReference);

        return new()
        {
            Source = crossReference.Source,
            AnidbAnimeID = crossReference.AnidbAnimeID,
            ProviderID = crossReference.ProviderID,
            MatchRating = crossReference.MatchRating,
        };
    }

    /// <summary>
    ///   Reads a film link back into the shape it would be written as.
    /// </summary>
    /// <param name="crossReference">The link as it reads.</param>
    /// <returns>The same link, ready to write.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="crossReference" /> is <c>null</c>.
    /// </exception>
    public static MetadataMovieLinkData ToLinkData(this IMetadataMovieCrossReference crossReference)
    {
        ArgumentNullException.ThrowIfNull(crossReference);

        return new()
        {
            Source = crossReference.Source,
            AnidbAnimeID = crossReference.AnidbAnimeID,
            AnidbEpisodeID = crossReference.AnidbEpisodeID,
            ProviderID = crossReference.ProviderID,
            MatchRating = crossReference.MatchRating,
        };
    }

    /// <summary>
    ///   Reads an episode link back into the shape it would be written as.
    /// </summary>
    /// <param name="crossReference">The link as it reads.</param>
    /// <returns>The same link, ready to write.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="crossReference" /> is <c>null</c>.
    /// </exception>
    public static MetadataEpisodeLinkData ToLinkData(this IMetadataEpisodeCrossReference crossReference)
    {
        ArgumentNullException.ThrowIfNull(crossReference);

        return new()
        {
            Source = crossReference.Source,
            AnidbAnimeID = crossReference.AnidbAnimeID,
            AnidbEpisodeID = crossReference.AnidbEpisodeID,
            ProviderID = crossReference.ProviderID,
            ProviderParentID = crossReference.ProviderParentID,
            SeasonID = crossReference.SeasonID,
            SeasonNumber = crossReference.SeasonNumber,
            EpisodeNumber = crossReference.EpisodeNumber,
            MatchRating = crossReference.MatchRating,
        };
    }

    /// <summary>
    ///   Turns a match into the link it stands for.
    /// </summary>
    /// <remarks>
    ///   A match with nothing on the other side becomes a link to no entry,
    ///   which is how an episode is recorded as deliberately linked to nothing.
    /// </remarks>
    /// <param name="match">The match, as the matching engine worked it out.</param>
    /// <param name="source">
    ///   The source being matched against, which a match with no candidate
    ///   cannot name for itself.
    /// </param>
    /// <returns>The link the match stands for.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="match" /> is <c>null</c>.
    /// </exception>
    public static MetadataEpisodeLinkData ToLinkData(this EpisodeMatch match, MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(match);

        return new()
        {
            Source = source,
            AnidbAnimeID = match.AnidbEpisode.AnidbAnimeID,
            AnidbEpisodeID = match.AnidbEpisode.AnidbID,
            ProviderID = match.Candidate?.ID,
            ProviderParentID = match.Candidate?.SeriesID,
            SeasonID = match.Candidate?.SeasonID,
            SeasonNumber = match.Candidate?.SeasonNumber,
            EpisodeNumber = match.Candidate?.EpisodeNumber,
            MatchRating = match.Rating,
        };
    }
}
