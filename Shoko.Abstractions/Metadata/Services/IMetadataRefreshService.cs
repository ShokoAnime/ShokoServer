using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Providers;

namespace Shoko.Abstractions.Metadata.Services;

/// <summary>
///   Asks the metadata providers for work: refreshes, image downloads and
///   auto-searches, run by the core's jobs for each provider.
/// </summary>
/// <remarks>
///   Every call goes to the provider enabled for the source and kind of
///   entry, and returns once the work is queued unless asked to run at once.
///   A refresh that is not forced skips an entry refreshed within the last
///   hour, and one lock per entry keeps its refreshes, purge and image
///   linking from overlapping.
/// </remarks>
public interface IMetadataRefreshService
{
    #region Refresh

    /// <summary>
    ///   Queue a refresh of one series, film or collection.
    /// </summary>
    /// <remarks>
    ///   A series or film is refreshed only while something links to it, and a
    ///   collection only while it is stored, unless the options say somebody
    ///   asked for it (<see cref="MetadataRefreshReason.Requested"/>). Forcing a
    ///   refresh does not count as asking for it.
    /// </remarks>
    /// <param name="entryID">The series, film or collection.</param>
    /// <param name="force">
    ///   Whether to refresh it however recently it last was. A forced refresh
    ///   is queued ahead of the rest.
    /// </param>
    /// <param name="options">
    ///   What to fetch, or <see langword="null"/> for a full refresh that goes
    ///   by the settings and downloads the images.
    /// </param>
    /// <param name="immediate">
    ///   Whether to run the refresh now and wait for it, rather than queueing
    ///   it. Nothing runs while the provider is paused.
    /// </param>
    /// <param name="prioritize">
    ///   Whether to queue the refresh ahead of the rest even though it is not
    ///   forced, such as for somebody waiting on it while the provider is
    ///   paused.
    /// </param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   <see langword="true"/> when the refresh was queued or ran;
    ///   <see langword="false"/> when no enabled provider refreshes the entry,
    ///   or it was asked to run at once while the provider is paused.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="entryID"/> is <see langword="null"/>.</exception>
    Task<bool> RefreshEntry(
        MetadataGuid entryID,
        bool force = false,
        MetadataRefreshOptions? options = null,
        bool immediate = false,
        bool prioritize = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///   Queue a refresh of everything an anime links to, from every enabled
    ///   provider or from one source.
    /// </summary>
    /// <remarks>
    ///   A source the anime has no links on is left alone: finding something
    ///   new is auto-linking's job, not this.
    /// </remarks>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="source">One source, or every source when left out.</param>
    /// <param name="force">
    ///   Whether to refresh every entry however recently it last was. A
    ///   forced refresh is queued apart from a scheduled one and ahead of the
    ///   rest.
    /// </param>
    /// <param name="options">
    ///   What to fetch, or <see langword="null"/> for a full refresh that goes
    ///   by the settings and downloads the images.
    /// </param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>How many providers were asked.</returns>
    Task<int> RefreshForAnime(
        int anidbAnimeID,
        MetadataSource? source = null,
        bool force = false,
        MetadataRefreshOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///   Queue a refresh of every series and film linked from a source to an
    ///   anime in the library, and of the source's stored collections.
    /// </summary>
    /// <remarks>
    ///   Each entry is queued once, however many anime link to it, for the
    ///   first anime that does, and is refreshed only if it is still linked
    ///   when its turn comes, whatever the options' reason.
    /// </remarks>
    /// <param name="source">The source.</param>
    /// <param name="force">Whether to refresh every entry however recently it last was.</param>
    /// <param name="options">
    ///   What to fetch, or <see langword="null"/> for a full refresh that goes
    ///   by the settings and downloads the images.
    /// </param>
    /// <param name="entityType">
    ///   Refresh only series, only films or only collections; left out, all
    ///   three.
    /// </param>
    /// <param name="progress">Told how far the work is, from 0 to 100, or <see langword="null"/> for no reports.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>How many refreshes were queued.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    Task<int> RefreshAllLinked(
        MetadataSource source,
        bool force = false,
        MetadataRefreshOptions? options = null,
        MetadataEntityType? entityType = null,
        IProgress<decimal>? progress = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///   Whether an entry is being refreshed or purged right now.
    /// </summary>
    /// <param name="entryID">The series, film or collection.</param>
    /// <returns><see langword="true"/> while its refresh or purge job runs.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entryID"/> is <see langword="null"/>.</exception>
    bool IsRefreshing(MetadataGuid entryID);

    /// <summary>
    ///   Wait for an entry's running refresh or purge to end, so what is read
    ///   next is what the provider wrote.
    /// </summary>
    /// <remarks>
    ///   Returns at once when nothing runs for the entry. Image downloads are
    ///   not waited for.
    /// </remarks>
    /// <param name="entryID">The series, film or collection.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>
    ///   <see langword="true"/> when there was a refresh or purge to wait for,
    ///   so a copy read before the call may be stale.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="entryID"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled while waiting.</exception>
    Task<bool> WaitForRefresh(MetadataGuid entryID, CancellationToken cancellationToken = default);

    /// <summary>
    ///   When an entry was last refreshed in full without failing.
    /// </summary>
    /// <remarks>
    ///   What the core's freshness check reads; an entry fetched only in part
    ///   has not been refreshed yet. Implementations should override it: the
    ///   default answers <see langword="null"/>, so every linked entry looks
    ///   never refreshed and is refreshed again at once.
    /// </remarks>
    /// <param name="entryID">The series, film or collection.</param>
    /// <returns>The time, or <see langword="null"/> when it never was.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entryID"/> is <see langword="null"/>.</exception>
    DateTime? GetLastRefreshedAt(MetadataGuid entryID)
        => null;

    #endregion

    #region Images

    /// <summary>
    ///   Queue the linking and download of one entry's images, and of what is
    ///   stored under it and credited on it.
    /// </summary>
    /// <remarks>
    ///   The image contributors enabled for the entry are queued after the
    ///   owner's image job has run, or at once when no enabled provider
    ///   supplies the images of the entry, such as for an AniDB anime.
    /// </remarks>
    /// <param name="entryID">The series, film or collection.</param>
    /// <param name="force">Whether to download the wanted images again even when they are there.</param>
    /// <param name="immediate">
    ///   Whether to run the job now and wait for it, rather than queueing it.
    ///   Nothing runs while the provider is paused.
    /// </param>
    /// <param name="prioritize">
    ///   Whether to queue the job ahead of the rest even though it is not
    ///   forced, such as for somebody waiting on it while the provider is
    ///   paused.
    /// </param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   <see langword="true"/> when the job was queued or ran, or a
    ///   contributor's job was queued; <see langword="false"/> when no enabled
    ///   provider or contributor supplies images for the entry, or it was
    ///   asked to run at once while the provider is paused.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="entryID"/> is <see langword="null"/>.</exception>
    Task<bool> DownloadImages(
        MetadataGuid entryID,
        bool force = false,
        bool immediate = false,
        bool prioritize = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///   Queue the images of every series and film an anime links to, from
    ///   every enabled provider that supplies images or from one source.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="source">One source, or every source when left out.</param>
    /// <param name="force">Whether to download the wanted images again even when they are there.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>How many image jobs were queued.</returns>
    Task<int> DownloadImagesForAnime(int anidbAnimeID, MetadataSource? source = null, bool force = false, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Queue the images of every series and film linked from a source to an
    ///   anime in the library.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="force">Whether to download the wanted images again even when they are there.</param>
    /// <param name="progress">Told how far the work is, from 0 to 100, or <see langword="null"/> for no reports.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>How many image jobs were queued.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    Task<int> DownloadAllImages(MetadataSource source, bool force = false, IProgress<decimal>? progress = null, CancellationToken cancellationToken = default);

    #endregion

    #region Auto-search

    /// <summary>
    ///   Queue a search by a source's auto-linker for one anime.
    /// </summary>
    /// <remarks>
    ///   A search that is not forced respects the admin's decision to
    ///   auto-link the source, the anime's own veto and the links it already
    ///   has, as the search for a new anime does.
    /// </remarks>
    /// <param name="source">The source to search.</param>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="force">
    ///   Whether a person asked, which goes ahead regardless of the setting,
    ///   the veto and the links. What a forced search takes replaces every
    ///   link the anime has on the source, verified ones and episode links
    ///   included; when it takes nothing, every link is left as it was.
    /// </param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   <see langword="true"/> when the search was queued;
    ///   <see langword="false"/> when the source has no auto-linker or it is
    ///   not configured.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    Task<bool> AutoSearch(MetadataSource source, int anidbAnimeID, bool force = false, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Queue a search by a source's auto-linker for every anime in the
    ///   library.
    /// </summary>
    /// <param name="source">The source to search.</param>
    /// <param name="force">
    ///   Whether to search regardless of the setting and the vetoes; left
    ///   off, only while the source auto-links and only the anime not left
    ///   alone. Either way only the anime not linked on the source yet are
    ///   searched, and no link is replaced.
    /// </param>
    /// <param name="progress">Told how far the work is, from 0 to 100, or <see langword="null"/> for no reports.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   How many searches were queued, none while the auto-linker is not
    ///   configured.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    Task<int> AutoSearchAll(MetadataSource source, bool force = false, IProgress<decimal>? progress = null, CancellationToken cancellationToken = default);

    #endregion

    #region Pausing

    /// <summary>
    ///   Whether the providers of a source can take work right now, and if not,
    ///   why and until when.
    /// </summary>
    /// <remarks>
    ///   Read from the enabled providers claiming the source that implement
    ///   <see cref="IPausableMetadataProvider"/>. While one of them is paused,
    ///   its jobs wait in the queue.
    /// </remarks>
    /// <param name="source">The source.</param>
    /// <returns>
    ///   The status of the paused provider that expects to resume last, or
    ///   <see cref="MetadataProviderPauseStatus.NotPaused"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    MetadataProviderPauseStatus GetPauseStatus(MetadataSource source);

    /// <summary>
    ///   Raised whenever a provider reports that it was paused or resumed.
    /// </summary>
    event EventHandler? PauseStatusChanged;

    #endregion
}
