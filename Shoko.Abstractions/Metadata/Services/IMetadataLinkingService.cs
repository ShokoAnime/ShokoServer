using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Search;
using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Abstractions.Metadata.Services;

/// <summary>
///   The one way to make, break and correct a link between a Shoko entry and
///   a provider's, whatever the source.
/// </summary>
/// <remarks>
///   Routes each call by source, to the core's own linking or to the source's
///   <see cref="IMetadataSeriesLinkingProvider"/> or
///   <see cref="IMetadataMovieLinkingProvider"/>. Nothing here queues a
///   refresh of what it links; the caller asks
///   <see cref="IMetadataRefreshService.RefreshEntry"/> when it wants one.
/// </remarks>
public interface IMetadataLinkingService
{
    /// <summary>
    ///   The sources that can be linked at all, and the entity types each one
    ///   accepts.
    /// </summary>
    /// <remarks>
    ///   A source that answers lookups may still refuse links.
    /// </remarks>
    IReadOnlyDictionary<MetadataSource, IReadOnlySet<MetadataEntityType>> LinkableEntityTypes { get; }

    #region Events

    /// <summary>
    ///   Raised once per write that added, removed, replaced or re-rated
    ///   links, of any source and at any level, with every link it changed
    ///   and why.
    /// </summary>
    /// <remarks>
    ///   Covers writes made through this service and those made straight to
    ///   <see cref="Storage.IMetadataCrossReferenceStore"/>. One call is one
    ///   event, whatever it wrote along the way. Reordering links and filling
    ///   in an episode link's season and numbers raise nothing. Handlers run
    ///   on the writer's thread after the write; an exception is logged.
    /// </remarks>
    event EventHandler<MetadataLinksChangedEventArgs>? LinksChanged
    {
        add { }
        remove { }
    }

    #endregion

    #region Search

    /// <summary>
    ///   Search a source for series a user might link to.
    /// </summary>
    /// <remarks>
    ///   Nothing is fetched or stored by searching.
    /// </remarks>
    /// <param name="source">The source to search.</param>
    /// <param name="options">What to search for.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   The page asked for, and how many results there are in total.
    /// </returns>
    /// <exception cref="System.NotSupportedException">
    ///   Thrown when that source cannot be searched for series.
    /// </exception>
    Task<(IReadOnlyList<MetadataSeriesSearchResult> Page, int TotalCount)> SearchSeries(MetadataSource source, MetadataSearchOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Search a source for films a user might link to.
    /// </summary>
    /// <remarks>
    ///   Nothing is fetched or stored by searching.
    /// </remarks>
    /// <param name="source">The source to search.</param>
    /// <param name="options">What to search for.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   The page asked for, and how many results there are in total.
    /// </returns>
    /// <exception cref="System.NotSupportedException">
    ///   Thrown when that source cannot be searched for films.
    /// </exception>
    Task<(IReadOnlyList<MetadataMovieSearchResult> Page, int TotalCount)> SearchMovies(MetadataSource source, MetadataSearchOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Look one series up by its ID at its source, the way a search would
    ///   have offered it.
    /// </summary>
    /// <remarks>
    ///   Goes to the enabled provider that links series on the ID's source.
    ///   Nothing is fetched into the stores or linked by looking.
    /// </remarks>
    /// <param name="seriesID">The series, of the <c>series</c> kind.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   The series, or <see langword="null"/> when the source has none by
    ///   that ID.
    /// </returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="seriesID"/> is <see langword="null"/>.</exception>
    /// <exception cref="System.NotSupportedException">
    ///   Thrown when no enabled provider links series on that source, or it
    ///   does not look them up.
    /// </exception>
    /// <exception cref="MetadataProviderUnavailableException">The source cannot be reached for now.</exception>
    Task<MetadataSeriesSearchResult?> LookupSeries(MetadataGuid seriesID, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Look one film up by its ID at its source, the way a search would have
    ///   offered it.
    /// </summary>
    /// <remarks>
    ///   Goes to the enabled provider that links films on the ID's source.
    ///   Nothing is fetched into the stores or linked by looking.
    /// </remarks>
    /// <param name="movieID">The film, of the <c>movie</c> kind.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   The film, or <see langword="null"/> when the source has none by that
    ///   ID.
    /// </returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="movieID"/> is <see langword="null"/>.</exception>
    /// <exception cref="System.NotSupportedException">
    ///   Thrown when no enabled provider links films on that source, or it
    ///   does not look them up.
    /// </exception>
    /// <exception cref="MetadataProviderUnavailableException">The source cannot be reached for now.</exception>
    Task<MetadataMovieSearchResult?> LookupMovie(MetadataGuid movieID, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Show what a source's auto-linking would do for an anime, without
    ///   linking anything.
    /// </summary>
    /// <remarks>
    ///   Runs the matching of the source's configured auto-linker now, not in
    ///   a job, and adds the core's refusals: a kind the admin turned off, or
    ///   an entry that cannot be linked to the anime. The same entry may come
    ///   once per <see cref="MetadataAutoLinkCandidate.Origin"/>. A turned-down
    ///   candidate is linked by hand through <see cref="AddSeriesLink"/> or
    ///   <see cref="AddMovieLink"/>.
    /// </remarks>
    /// <param name="source">The source to ask.</param>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   The candidates, best first, those not taken carrying why; an empty
    ///   list when nothing was found. The provider's candidates are followed
    ///   by the anime's current links, any from a prequel (never taken), then
    ///   the entries its AniDB resources or links on other sources name. As in
    ///   a forced search, the first of those left is taken when nothing the
    ///   search took competes with it (for a film, only candidates for its
    ///   episode or the whole anime compete), or only candidates the matching
    ///   engine rates strictly below it, which are then turned down as
    ///   <see cref="MatchRejectionReason.Outranked"/>. A tie keeps the search's
    ///   pick.
    /// </returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="System.NotSupportedException">
    ///   Thrown when no provider is set to auto-link that source.
    /// </exception>
    /// <exception cref="MetadataProviderUnavailableException">The source cannot be reached for now.</exception>
    Task<IReadOnlyList<MetadataAutoLinkCandidate>> PreviewAutoLink(MetadataSource source, int anidbAnimeID, CancellationToken cancellationToken = default);

    /// <summary>
    ///   The entries of a source that the anime's links on other sources name
    ///   as the anime's own, for its auto-linker to weigh as hints.
    /// </summary>
    /// <remarks>
    ///   Read from <see cref="IWithCrossSources.CrossSourceIDs"/> of every
    ///   stored series, movie and episode the anime is linked to on another
    ///   source. A named episode stands for its known
    ///   series, so a hint only places a series or a film; a film named by a
    ///   link for one AniDB episode stays on that episode.
    /// </remarks>
    /// <param name="source">The source whose entries to find.</param>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <returns>
    ///   The hints, each entry once with every link naming it, in the order
    ///   the anime's links were read; empty when none name the source.
    /// </returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    IReadOnlyList<MetadataAutoLinkHint> GetCrossSourceHints(MetadataSource source, int anidbAnimeID);

    #endregion

    #region Whole works

    /// <summary>
    ///   Link a provider's series to an anime.
    /// </summary>
    /// <remarks>
    ///   A replacing request (<see cref="MetadataSeriesLinkRequest.Additive"/>
    ///   off) also removes the episode links under each series it replaces. A
    ///   series then has the anime's episodes matched and saved, as
    ///   <see cref="MatchEpisodes"/> would, when the source links episodes; one
    ///   not stored yet is matched once its refresh writes it. No refresh is
    ///   queued: ask <see cref="IMetadataRefreshService.RefreshEntry"/>.
    /// </remarks>
    /// <param name="request">What to link, and how.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   <see langword="true"/> if the link now exists.
    /// </returns>
    /// <exception cref="System.ArgumentException">
    ///   Thrown when the request names no entry, or one on another source or
    ///   of another kind.
    /// </exception>
    /// <exception cref="System.NotSupportedException">
    ///   Thrown when nothing can link that source and entity type.
    /// </exception>
    Task<bool> AddSeriesLink(MetadataSeriesLinkRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Link a provider's film to the episode standing for it.
    /// </summary>
    /// <remarks>
    ///   No refresh is queued: a caller linking a film that is not stored yet
    ///   asks <see cref="IMetadataRefreshService.RefreshEntry"/> for one.
    /// </remarks>
    /// <param name="request">What to link, and how.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   <see langword="true"/> if the link now exists.
    /// </returns>
    /// <exception cref="System.ArgumentException">
    ///   Thrown when the request names no film, or an entry on another source
    ///   or of another kind.
    /// </exception>
    /// <exception cref="System.NotSupportedException">
    ///   Thrown when that source does not link films.
    /// </exception>
    Task<bool> AddMovieLink(MetadataEpisodeLinkRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Break a link between a provider's series and an anime.
    /// </summary>
    /// <remarks>
    ///   A series link takes the anime's episode links under it along, but not
    ///   film links; episode links naming no series go with the last series
    ///   link for the source. A request naming a film removes that film's
    ///   series link and its film links on the anime's episodes instead. Works
    ///   whether or not a provider for the source is enabled.
    /// </remarks>
    /// <param name="request">
    ///   What to unlink. <see cref="MetadataSeriesLinkRequest.Purge"/> also
    ///   queues a purge of the entry, which goes ahead once nothing links to
    ///   it; <see cref="MetadataSeriesLinkRequest.DisableAutoLinking"/> also
    ///   tells the source to leave the anime alone.
    /// </param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   <see langword="true"/> if the link is gone, or <see langword="false"/>
    ///   if there was nothing to remove.
    /// </returns>
    /// <exception cref="System.ArgumentException">
    ///   Thrown when the entry named is on another source or of another kind.
    /// </exception>
    Task<bool> RemoveSeriesLink(MetadataSeriesLinkRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Break a link between a provider's film and the episode standing for
    ///   it.
    /// </summary>
    /// <remarks>
    ///   Works whether or not a provider for the source is enabled.
    /// </remarks>
    /// <param name="request">
    ///   What to unlink. <see cref="MetadataEpisodeLinkRequest.Purge"/> also
    ///   queues a purge of the film, which goes ahead once nothing links to
    ///   it; <see cref="MetadataEpisodeLinkRequest.DisableAutoLinking"/> also
    ///   tells the source to leave the anime alone.
    /// </param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   <see langword="true"/> if the link is gone.
    /// </returns>
    /// <exception cref="System.ArgumentException">
    ///   Thrown when the film named is on another source.
    /// </exception>
    Task<bool> RemoveMovieLink(MetadataEpisodeLinkRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Whether a source has been told to leave an anime alone.
    /// </summary>
    /// <remarks>
    ///   A per-anime veto on automatic linking, separate from whether the
    ///   source is enabled at all.
    /// </remarks>
    /// <param name="series">The Shoko series.</param>
    /// <param name="source">The source being asked about.</param>
    /// <returns>
    ///   <see langword="true"/> when that source must not link this anime on
    ///   its own.
    /// </returns>
    bool IsAutoLinkingDisabled(IShokoSeries series, MetadataSource source);

    /// <summary>
    ///   Tell a source to leave an anime alone, or to stop leaving it alone.
    /// </summary>
    /// <param name="series">The Shoko series.</param>
    /// <param name="source">The source being told.</param>
    /// <param name="disabled">
    ///   <see langword="true"/> to stop it linking this anime on its own.
    /// </param>
    /// <exception cref="System.NotSupportedException">
    ///   Thrown when the decision cannot be recorded for that source.
    /// </exception>
    void SetAutoLinkingDisabled(IShokoSeries series, MetadataSource source, bool disabled);

    /// <summary>
    ///   Queue a search in which a source's auto-linker matches an anime and
    ///   links what it finds.
    /// </summary>
    /// <remarks>
    ///   Goes ahead even where <see cref="IsAutoLinkingDisabled"/> says no,
    ///   since a person asked. What the provider takes replaces every link the
    ///   anime has on the source, verified and episode links included; when it
    ///   takes nothing, fails, or nothing can be written, the links stay.
    /// </remarks>
    /// <param name="source">The source to ask.</param>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   <see langword="true"/> once the search is queued, which says nothing
    ///   of what it will link, if anything; <see langword="false"/> when it
    ///   was not queued, as for an anime ID of <c>0</c> or less or while the
    ///   auto-linker is not configured.
    /// </returns>
    /// <exception cref="System.NotSupportedException">
    ///   Thrown when no provider is set to auto-link that source.
    /// </exception>
    Task<bool> AutoLink(MetadataSource source, int anidbAnimeID, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Tell a source to leave every anime in the library alone, or to stop
    ///   leaving any of them alone.
    /// </summary>
    /// <param name="source">The source being told.</param>
    /// <param name="disabled">
    ///   <see langword="true"/> to stop it linking any anime on its own;
    ///   <see langword="false"/> to lift every veto.
    /// </param>
    /// <returns>How many anime changed.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    int ResetAutoLinkingState(MetadataSource source, bool disabled = false);

    #endregion

    #region Match ratings

    /// <summary>
    ///   Set how far links are trusted, verifying them unless told otherwise,
    ///   without removing or matching anything.
    /// </summary>
    /// <remarks>
    ///   Each link is found again by its level, source, AniDB entry and
    ///   provider entry, and only its rating changes: its place, its writer
    ///   and an episode link's series, season and numbers stay. Links no
    ///   longer stored, and season links, which are worked out from the
    ///   episode links, are skipped. Works whether or not a provider for the
    ///   source is enabled, as the removals do.
    /// </remarks>
    /// <param name="links">
    ///   The series, film or episode links, of any source, as read back from
    ///   the store or the metadata service.
    /// </param>
    /// <param name="matchRating">
    ///   The rating to give them, <see cref="MatchRating.UserVerified"/> when
    ///   left out.
    /// </param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   The links found, as stored now, whether or not their rating changed.
    /// </returns>
    /// <exception cref="System.ArgumentNullException">
    ///   <paramref name="links"/> is or holds <see langword="null"/>.
    /// </exception>
    /// <exception cref="System.ArgumentOutOfRangeException">
    ///   <paramref name="matchRating"/> is not a defined rating.
    /// </exception>
    Task<IReadOnlyList<IMetadataCrossReference>> SetMatchRating(
        IEnumerable<IMetadataCrossReference> links,
        MatchRating matchRating = MatchRating.UserVerified,
        CancellationToken cancellationToken = default
    );

    #endregion

    #region Bulk removal

    /// <summary>
    ///   Break every link a source has, at the levels asked for.
    /// </summary>
    /// <remarks>
    ///   Works whether or not a provider for the source is enabled, so a
    ///   source can be cleared out after its plugin is gone. No anime is told
    ///   to be left alone.
    /// </remarks>
    /// <param name="source">The source.</param>
    /// <param name="removeSeriesLinks">
    ///   Whether to remove the series links and every episode link.
    /// </param>
    /// <param name="removeMovieLinks">
    ///   Whether to remove the film links, a film's claim on a whole anime
    ///   included.
    /// </param>
    /// <param name="purge">
    ///   Whether to also queue a purge of every series and film the removed
    ///   links pointed at, which goes ahead once nothing links to it.
    /// </param>
    /// <param name="progress">Told how far the work is, from 0 to 100, or <see langword="null"/> for no reports.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>How many links were removed.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    Task<int> RemoveAllLinks(
        MetadataSource source,
        bool removeSeriesLinks = true,
        bool removeMovieLinks = true,
        bool purge = false,
        IProgress<decimal>? progress = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///   Break an anime's links on a source, at one level or all of them.
    /// </summary>
    /// <remarks>
    ///   The series level takes the episode links under the removed series
    ///   along, and those naming no series once no series link is left, as
    ///   <see cref="RemoveSeriesLink"/> does. The film level covers a film's
    ///   claim on the whole anime and the film links on its episodes.
    /// </remarks>
    /// <param name="source">The source.</param>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="entityType">
    ///   <c>series</c>, <c>movie</c> or <c>episode</c> for one level, or
    ///   <see langword="null"/> for every link the anime has on the source.
    /// </param>
    /// <param name="purge">
    ///   Whether to also queue a purge of every series and film the removed
    ///   links pointed at, which goes ahead once nothing links to it.
    /// </param>
    /// <param name="disableAutoLinking">Whether to also tell the source to leave the anime alone, when anything was removed.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>How many links were removed.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    Task<int> RemoveLinksForAnime(
        MetadataSource source,
        int anidbAnimeID,
        MetadataEntityType? entityType = null,
        bool purge = false,
        bool disableAutoLinking = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///   Break one episode's links on a source, its film links, its episode
    ///   links or both.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="anidbEpisodeID">The AniDB episode ID.</param>
    /// <param name="entityType">
    ///   <c>movie</c> or <c>episode</c> for one level, or
    ///   <see langword="null"/> for both.
    /// </param>
    /// <param name="purge">
    ///   Whether to also queue a purge of every film the removed links named
    ///   and every series they pointed into, which goes ahead once nothing
    ///   links to it.
    /// </param>
    /// <param name="disableAutoLinking">Whether to also tell the source to leave the episode's anime alone, when anything was removed.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>How many links were removed.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    Task<int> RemoveLinksForEpisode(
        MetadataSource source,
        int anidbEpisodeID,
        MetadataEntityType? entityType = null,
        bool purge = false,
        bool disableAutoLinking = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///   Break every link pointing at one provider entry, from whichever
    ///   anime it is.
    /// </summary>
    /// <remarks>
    ///   A series takes the episode links pointing into it along. No anime is
    ///   told to be left alone.
    /// </remarks>
    /// <param name="providerID">The provider's series, film or episode.</param>
    /// <param name="purge">
    ///   Whether to also queue a purge of the series or film, which goes ahead
    ///   once nothing links to it.
    /// </param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>How many links were removed.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="providerID"/> is <see langword="null"/>.</exception>
    Task<int> RemoveLinksTo(MetadataGuid providerID, bool purge = false, CancellationToken cancellationToken = default);

    #endregion

    #region Episodes

    /// <summary>
    ///   Point a Shoko episode at a provider's.
    /// </summary>
    /// <remarks>
    ///   Fetches nothing. A <c>null</c>
    ///   <see cref="MetadataEpisodeLinkRequest.ProviderID"/> records the
    ///   episode as deliberately linked to nothing, which matching respects;
    ///   it replaces the episode's other links on the source whatever
    ///   <see cref="MetadataEpisodeLinkRequest.Additive"/> says, and a kept
    ///   link added later replaces it.
    /// </remarks>
    /// <param name="request">
    ///   What to link, and how. The link records the provider series, season
    ///   and numbers as the stored data has them, and is placed at
    ///   <see cref="MetadataEpisodeLinkRequest.Ordering"/> when given. For an
    ///   episode not stored yet the series is
    ///   <see cref="MetadataEpisodeLinkRequest.ProviderSeriesID"/>, with no
    ///   season or numbers until the series is stored; no refresh is queued,
    ///   so ask <see cref="IMetadataRefreshService.RefreshEntry"/> for it.
    /// </param>
    /// <returns>
    ///   <see langword="true"/> if the link now exists.
    /// </returns>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <exception cref="System.NotSupportedException">
    ///   Thrown when that source does not link episodes.
    /// </exception>
    /// <exception cref="System.ArgumentException">
    ///   Thrown when the episode or the series is on another source, or of
    ///   another kind.
    /// </exception>
    Task<bool> SetEpisodeLink(MetadataEpisodeLinkRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Point a Shoko episode at a provider's, without building a request.
    /// </summary>
    /// <param name="source">The source the episode belongs to.</param>
    /// <param name="anidbEpisodeID">The AniDB episode ID.</param>
    /// <param name="providerEpisodeID">
    ///   The provider episode, on <paramref name="source"/>, or <c>null</c> to
    ///   record the episode as deliberately linked to nothing.
    /// </param>
    /// <param name="additive">Whether to keep the links already there.</param>
    /// <param name="ordering">
    ///   Where this link sits when an episode carries several.
    /// </param>
    /// <param name="providerSeriesID">
    ///   The provider series the episode sits in, for an episode the core's
    ///   series store does not hold yet, or <c>null</c>.
    /// </param>
    /// <returns>
    ///   <see langword="true"/> if the link now exists.
    /// </returns>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <exception cref="System.NotSupportedException">
    ///   Thrown when that source does not link episodes.
    /// </exception>
    /// <exception cref="System.ArgumentException">
    ///   Thrown when the episode or the series is on another source, or of
    ///   another kind.
    /// </exception>
    Task<bool> SetEpisodeLink(
        MetadataSource source,
        int anidbEpisodeID,
        MetadataGuid? providerEpisodeID,
        bool additive = true,
        int? ordering = null,
        MetadataGuid? providerSeriesID = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///   Clear every episode link under an anime for a source.
    /// </summary>
    /// <remarks>
    ///   Refusing matching is recorded per episode: while the anime is linked
    ///   to a series of the source, each of its regular episodes and specials
    ///   is left deliberately linked to nothing, verified by a person, which
    ///   matching keeps. The anime's auto-linking veto is left as it was.
    ///   Works whether or not a provider for the source is enabled.
    /// </remarks>
    /// <param name="source">The source to clear.</param>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="allowAutoMatch">
    ///   Whether matching may fill them in again. <see langword="false"/> is a
    ///   person saying no.
    /// </param>
    /// <returns>
    ///   <see langword="true"/> if anything changed.
    /// </returns>
    /// <param name="cancellationToken">Cancels the work.</param>
    Task<bool> ResetEpisodeLinks(MetadataSource source, int anidbAnimeID, bool allowAutoMatch, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Work out which episodes line up, without committing to it unless
    ///   asked.
    /// </summary>
    /// <remarks>
    ///   The same matching a source runs for itself, returned rather than
    ///   written unless <paramref name="save"/> is set.
    /// </remarks>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="providerSeriesID">
    ///   The provider series to match into, whose source is the one matched
    ///   against.
    /// </param>
    /// <param name="providerSeasonID">
    ///   One season of it, when the source has seasons and only one is
    ///   wanted.
    /// </param>
    /// <param name="useExisting">
    ///   Whether to keep the links already there and only fill the gaps.
    /// </param>
    /// <param name="save">
    ///   Whether to write the result; otherwise nothing changes. A saved result
    ///   replaces the links of each episode the source decided on. Saving
    ///   queues no refresh, so a provider may save inside its own refresh.
    /// </param>
    /// <param name="considerOtherLinks">
    ///   Whether to leave the source's episodes that other anime are already
    ///   linked to out of the candidates, or <see langword="null"/> for the
    ///   source's own default.
    /// </param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   Every episode in scope and what it is lined up with: the links
    ///   already on record, kept as they are, and what the source came up with
    ///   for the rest, each at the place the source gave it. A link that has
    ///   not been written has no ID yet.
    /// </returns>
    /// <exception cref="System.NotSupportedException">
    ///   Thrown when that source does not link episodes.
    /// </exception>
    /// <exception cref="System.ArgumentException">
    ///   <paramref name="providerSeriesID"/> is not a series, or
    ///   <paramref name="providerSeasonID"/> is not a season of the same
    ///   source.
    /// </exception>
    Task<IReadOnlyList<IMetadataEpisodeCrossReference>> MatchEpisodes(
        int anidbAnimeID,
        MetadataGuid providerSeriesID,
        MetadataGuid? providerSeasonID = null,
        bool useExisting = false,
        bool save = false,
        bool? considerOtherLinks = null,
        CancellationToken cancellationToken = default
    );

    #endregion
}
