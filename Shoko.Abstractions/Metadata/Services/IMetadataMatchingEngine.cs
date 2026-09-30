using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Matching;
using Shoko.Abstractions.Metadata.Search;

namespace Shoko.Abstractions.Metadata.Services;

/// <summary>
///   Judges how well a source's data lines up with AniDB's.
/// </summary>
/// <remarks>
///   The matching the core does for its own sources, for providers that would
///   rather not write their own. It reads and writes nothing: it returns the
///   pairings and the caller stores them.
/// </remarks>
public interface IMetadataMatchingEngine
{
    /// <summary>
    ///   Judge how well each series a search returned lines up with an anime,
    ///   and say which one to take.
    /// </summary>
    /// <remarks>
    ///   Titles decide first, the start of the anime's regular broadcast
    ///   second, and the episode count (of the season lining up best) third;
    ///   the source's order settles the rest. Titles match exactly once
    ///   folded, ignoring the spaces between words of scripts written without
    ///   them (Japanese, Chinese).
    /// </remarks>
    /// <param name="anime">The anime being matched.</param>
    /// <param name="candidates">
    ///   What the source offered, in whatever order it offered them. A season
    ///   sent with its episodes (<see cref="MetadataSearchResultSeason.Episodes"/>)
    ///   is lined up with the anime's regular episodes by air date, a day
    ///   either way; when enough days line up (three, or all when the anime
    ///   aired on fewer) over nearly all of the season, its dates agree
    ///   whatever the years say and it wins ties before the episode count.
    ///   The alignment is reported in <see cref="SeriesMatch.EpisodeAlignment"/>.
    /// </param>
    /// <param name="options">
    ///   What the source searched with and what it is like. Left out, every
    ///   title of the anime is tried, adult candidates are rejected and one
    ///   entry is taken to hold every season.
    /// </param>
    /// <exception cref="System.ArgumentNullException">
    ///   Thrown when <paramref name="anime"/> or
    ///   <paramref name="candidates"/> is <see langword="null"/>.
    /// </exception>
    /// <returns>
    ///   One entry per candidate, best first. At most the first is taken,
    ///   with <see cref="MatchRejectionReason.None"/>, and only when its
    ///   title or date agreed; every other entry says why it was not, and
    ///   what was compared in <see cref="SeriesMatch.Details"/>. A date agrees
    ///   on the year, or when the first episode aired within three days of the
    ///   anime's first regular one. Between two rated alike, one begun more
    ///   than two months after the anime ended loses.
    /// </returns>
    IReadOnlyList<SeriesMatch> MatchSeries(IAnidbAnime anime, IReadOnlyList<MetadataSeriesSearchResult> candidates, SeriesMatchOptions? options = null);

    /// <summary>
    ///   Judge which of the films a search returned is the one an episode
    ///   stands for.
    /// </summary>
    /// <remarks>
    ///   Claimed against the episode standing for the film, and the anime is
    ///   passed too since the episode's own title (often "Complete Movie") is
    ///   rarely any help. Titles decide first, the release year second, and
    ///   the source's order settles the rest.
    /// </remarks>
    /// <param name="anime">The anime the film belongs to.</param>
    /// <param name="episode">The episode standing for a film.</param>
    /// <param name="candidates">
    ///   What the source offered, in whatever order it offered them.
    /// </param>
    /// <param name="options">
    ///   What the source searched with. Left out, every title of the anime
    ///   and the episode is tried and adult candidates are rejected.
    /// </param>
    /// <exception cref="System.ArgumentNullException">
    ///   Thrown when <paramref name="anime"/>, <paramref name="episode"/> or
    ///   <paramref name="candidates"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="System.ArgumentException">
    ///   Thrown when the episode does not belong to the anime.
    /// </exception>
    /// <returns>
    ///   One entry per candidate, best first. At most the first is taken,
    ///   with <see cref="MatchRejectionReason.None"/>, and only when its
    ///   title or year agreed; every other entry says why it was not, and
    ///   what was compared in <see cref="MovieMatch.Details"/>. The year is
    ///   the anime's when the film is its only entry or its complete film,
    ///   and the episode's otherwise, regular broadcast dates first.
    /// </returns>
    IReadOnlyList<MovieMatch> MatchMovies(IAnidbAnime anime, IAnidbEpisode episode, IReadOnlyList<MetadataMovieSearchResult> candidates, MovieMatchOptions? options = null);

    /// <summary>
    ///   Line two sets of episodes up.
    /// </summary>
    /// <param name="anidbEpisodes">The AniDB episodes, as they come.</param>
    /// <param name="providerEpisodes">
    ///   The source's episodes, in whatever order it keeps them.
    /// </param>
    /// <param name="existing">
    ///   The links already on record for these episodes, which are honoured
    ///   rather than matched again. Left out, everything is matched afresh.
    /// </param>
    /// <param name="options">How to go about it.</param>
    /// <returns>
    ///   One entry per AniDB episode, including the ones nothing matched,
    ///   but for an episode an existing link settles outside the candidates
    ///   given (a person's link to nothing, or a link to an entry not among
    ///   them), which is left out so that saving the result leaves its links
    ///   alone.
    /// </returns>
    IReadOnlyList<EpisodeMatch> MatchEpisodes(
        IReadOnlyList<IAnidbEpisode> anidbEpisodes,
        IReadOnlyList<IEpisode> providerEpisodes,
        IReadOnlyList<IMetadataEpisodeCrossReference>? existing = null,
        EpisodeMatchOptions? options = null
    );
}
