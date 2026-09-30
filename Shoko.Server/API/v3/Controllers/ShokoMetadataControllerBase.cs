using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User.Services;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// What the routes hanging a source's links off a Shoko series or episode
/// share: finding the Shoko entry the user may see, refusing a source nothing
/// is linked to, and refreshing what they link.
/// </summary>
/// <param name="settingsProvider">The settings.</param>
/// <param name="logger">Logs refreshes refused or queued while a source is paused.</param>
/// <param name="userService">Tells who is asking.</param>
/// <param name="metadataService">Reads the Shoko entries and the linked ones.</param>
/// <param name="refreshService">Refreshes what is linked.</param>
public abstract class ShokoMetadataControllerBase(
    ISettingsProvider settingsProvider,
    ILogger logger,
    IUserService userService,
    IMetadataService metadataService,
    IMetadataRefreshService refreshService
) : BaseController(settingsProvider)
{
    #region Constants

    internal const string SeriesNotFound = "No Series entry for the given seriesID";

    internal const string EpisodeNotFound = "No Episode entry for the given episodeID";

    internal const string ForbiddenForUser = "Accessing Series is not allowed for the current user";

    #endregion

    #region Lookup

    /// <summary>
    /// The Shoko series a route names, when the user may see it and the
    /// source is one entries are linked to.
    /// </summary>
    /// <param name="seriesID">The Shoko series ID.</param>
    /// <param name="source">The source.</param>
    /// <param name="error">The answer to give when there is no series to work on.</param>
    /// <returns>The series, or <see langword="null"/> with <paramref name="error"/> set.</returns>
    protected IShokoSeries? GetSeries(int seriesID, MetadataSource source, out ActionResult? error)
    {
        error = RefuseNonTarget(source);
        if (error is not null)
            return null;

        if (metadataService.GetShokoSeriesByID(seriesID) is not { } series)
        {
            error = NotFound(SeriesNotFound);
            return null;
        }

        if (!MaySee(series))
        {
            error = Forbid(ForbiddenForUser);
            return null;
        }

        return series;
    }

    /// <summary>
    /// The Shoko episode a route names, when the user may see its series and
    /// the source is one entries are linked to.
    /// </summary>
    /// <param name="episodeID">The Shoko episode ID.</param>
    /// <param name="source">The source.</param>
    /// <param name="error">The answer to give when there is no episode to work on.</param>
    /// <returns>The episode, or <see langword="null"/> with <paramref name="error"/> set.</returns>
    protected IShokoEpisode? GetEpisode(int episodeID, MetadataSource source, out ActionResult? error)
    {
        error = RefuseNonTarget(source);
        if (error is not null)
            return null;

        if (metadataService.GetShokoEpisodeByID(episodeID) is not { } episode)
        {
            error = NotFound(EpisodeNotFound);
            return null;
        }

        if (metadataService.GetShokoSeriesByID(episode.ShokoSeriesID) is { } series && !MaySee(series))
        {
            error = Forbid(ForbiddenForUser);
            return null;
        }

        return episode;
    }

    /// <summary>
    /// Whether the user asking may see a series.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns><see langword="true"/> when they may, or when nobody is signed in to ask for.</returns>
    private bool MaySee(IShokoSeries series)
        => userService.GetUserFromHttpContext(HttpContext) is not { } user || user.IsAllowedToSee(series);

    /// <summary>
    /// Refuses a source nothing is linked to: AniDB, the hub everything is
    /// linked from, and the server's own sources.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>A problem to answer with, or <see langword="null"/> to go ahead.</returns>
    protected ActionResult? RefuseNonTarget(MetadataSource source)
        => MetadataSourceActions.IsLinkTarget(source)
            ? null
            : ValidationProblem($"{source.Name} is not linked to, so it has no links to work on here.", "source");

    /// <summary>
    /// A linked entry as it stands once a running refresh or purge of it has
    /// ended.
    /// </summary>
    /// <typeparam name="TMetadata">The entry's type.</typeparam>
    /// <param name="entryID">The entry.</param>
    /// <param name="refreshedWith">The series or movie the entry is refreshed with.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>The entry, or <see langword="null"/> when it is not stored.</returns>
    protected async Task<TMetadata?> Fresh<TMetadata>(MetadataGuid entryID, MetadataGuid refreshedWith, CancellationToken cancellationToken)
        where TMetadata : class, IMetadata
    {
        await refreshService.WaitForRefresh(refreshedWith, cancellationToken).ConfigureAwait(false);
        return metadataService.GetEntry<TMetadata>(entryID);
    }

    #endregion

    #region Refresh

    /// <summary>
    /// A refresh a person asked for, with the images.
    /// </summary>
    protected static MetadataRefreshOptions RequestedWithImages
        => new() { DownloadImages = true, Reason = MetadataRefreshReason.Requested };

    /// <summary>
    /// Whether a linked series or movie is stored and was refreshed in full
    /// at least once, rather than only fetched in part for somebody waiting
    /// on it.
    /// </summary>
    /// <param name="entryID">The series or movie.</param>
    /// <returns><see langword="true"/> when it needs no refresh on being linked.</returns>
    protected bool IsFullyRefreshed(MetadataGuid entryID)
        => refreshService.GetLastRefreshedAt(entryID) is not null && metadataService.GetEntry(entryID) is not null;

    /// <summary>
    /// Queues the refresh of a series or movie just linked: forced when asked
    /// for, otherwise only when it was never refreshed in full.
    /// </summary>
    /// <param name="entryID">The series or movie.</param>
    /// <param name="force">Whether a person asked for it to be refreshed.</param>
    /// <param name="cancellationToken">Cancels the queueing.</param>
    /// <returns><see langword="true"/> when a refresh was queued.</returns>
    protected async Task<bool> RefreshLinked(MetadataGuid entryID, bool force, CancellationToken cancellationToken)
    {
        if (!force && IsFullyRefreshed(entryID))
            return false;

        await refreshService.RefreshEntry(entryID, force, RequestedWithImages, cancellationToken: cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Answers <c>503 Service Unavailable</c> with a <c>Retry-After</c> while
    /// the source is paused, after queueing the work at the front when the
    /// caller did not ask to wait for it, so a caller knows nothing ran yet.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="queue">Queues the work at the front.</param>
    /// <param name="description">What the work is, for the log.</param>
    /// <param name="immediate">Whether the caller wanted to wait for the work.</param>
    /// <returns>The answer while paused, or <see langword="null"/> to go ahead.</returns>
    protected async Task<ActionResult?> QueueWhenPaused(MetadataSource source, Func<Task> queue, string description, bool immediate)
    {
        var status = refreshService.GetPauseStatus(source);
        if (!status.IsPaused)
            return null;

        var seconds = MetadataPauseResponses.RetryAfterSeconds(status);
        if (immediate)
        {
            logger.LogInformation("{Source} is paused. {Work} was asked for at once and was refused; retry in about {Seconds} second(s).", source.Name, description, seconds);
        }
        else
        {
            logger.LogInformation("{Source} is paused. {Work} was queued and starts in about {Seconds} second(s).", source.Name, description, seconds);
            await queue().ConfigureAwait(false);
        }

        return MetadataPauseResponses.Paused(Response, source, status);
    }

    /// <summary>
    /// Runs a piece of work for each of several entries at once.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <param name="work">The work for one entry.</param>
    /// <returns>A task that completes once all of it is done.</returns>
    protected static Task ForEach(IEnumerable<MetadataGuid> entries, Func<MetadataGuid, Task> work)
        => Task.WhenAll(entries.Distinct().Select(work));

    #endregion
}
