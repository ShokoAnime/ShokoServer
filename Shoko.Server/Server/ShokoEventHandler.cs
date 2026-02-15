using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Events;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Video;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server;

public class ShokoEventHandler : IShokoEventHandler
{
    public event EventHandler<FileEventArgs>? FileDeleted;

    public event EventHandler<SeriesInfoUpdatedEventArgs>? SeriesUpdated;

    public event EventHandler<SeasonInfoUpdatedEventArgs>? SeasonUpdated;

    public event EventHandler<EpisodeInfoUpdatedEventArgs>? EpisodeUpdated;

    public event EventHandler<MovieInfoUpdatedEventArgs>? MovieUpdated;

    public event EventHandler<AvdumpEventArgs>? AVDumpEvent;

    public event EventHandler? Starting;

    public event EventHandler? Started;

    public event EventHandler? Shutdown;

    private static ShokoEventHandler? _instance;

    public static ShokoEventHandler Instance => _instance ??= new();

    public void OnFileDeleted(IManagedFolder folder, IVideoFile vlp, IVideo vl)
    {
        var path = vlp.RelativePath;
        var xrefs = vl.CrossReferences;
        var episodes = xrefs
            .Select(x => x.ShokoEpisode)
            .WhereNotNull()
            .ToList();
        var series = xrefs
            .DistinctBy(x => x.AnidbAnimeID)
            .Select(x => x.ShokoSeries)
            .WhereNotNull()
            .ToList();
        var groups = series
            .DistinctBy(a => a.ParentGroupID)
            .Select(a => a.ParentGroup)
            .WhereNotNull()
            .ToList();
        FileDeleted?.Invoke(null, new(path, folder, vlp, vl, episodes, series, groups));
    }

    public void OnSeriesUpdated(AniDB_Anime anime, UpdateReason reason, IEnumerable<KeyValuePair<AniDB_Episode, UpdateReason>>? episodes = null)
        => OnSeriesUpdated(anime, reason, [], episodes?.Select(e => ((IEpisode)e.Key, e.Value)));

    public void OnSeriesUpdated(TMDB_Show show, UpdateReason reason, IEnumerable<KeyValuePair<TMDB_Season, UpdateReason>>? seasons = null, IEnumerable<KeyValuePair<TMDB_Episode, UpdateReason>>? episodes = null)
        => OnSeriesUpdated(show, reason, seasons?.Select(s => ((ISeason)s.Key, s.Value)), episodes?.Select(e => ((IEpisode)e.Key, e.Value)));

    public void OnSeriesUpdated(ISeries series, UpdateReason reason, IEnumerable<(ISeason season, UpdateReason reason)>? seasons = null, IEnumerable<(IEpisode episode, UpdateReason reason)>? episodes = null)
    {
        ArgumentNullException.ThrowIfNull(series, nameof(series));
        var seasonEvents = seasons?.Select(s => new SeasonInfoUpdatedEventArgs(series, s.season, s.reason)).ToList() ?? [];
        var episodeEvents = episodes?.Select(e => new EpisodeInfoUpdatedEventArgs(series, e.episode, e.reason)).ToList() ?? [];
        SeriesUpdated?.Invoke(null, new(series, reason, seasonEvents, episodeEvents));
        foreach (var e in episodeEvents)
            EpisodeUpdated?.Invoke(null, e);
    }

    public void OnSeriesUpdated(IShokoSeries series, UpdateReason reason, IEnumerable<KeyValuePair<AnimeEpisode, UpdateReason>> episodes)
        => OnSeriesUpdated(series, reason, episodes.Select(e => ((IShokoEpisode)e.Key, e.Value)));

    public void OnSeriesUpdated(IShokoSeries series, UpdateReason reason, IEnumerable<(IShokoEpisode episode, UpdateReason reason)>? episodes = null)
    {
        ArgumentNullException.ThrowIfNull(series, nameof(series));
        var episodeEvents = episodes?.Select(e => new EpisodeInfoUpdatedEventArgs(series, e.episode, e.reason)).ToList() ?? [];
        SeriesUpdated?.Invoke(null, new(series, reason, [], episodeEvents));
        foreach (var e in episodeEvents)
            EpisodeUpdated?.Invoke(null, e);
    }

    public void OnSeasonUpdated(IShokoSeries series, IShokoSeason season, UpdateReason reason)
    {
        ArgumentNullException.ThrowIfNull(series, nameof(series));
        ArgumentNullException.ThrowIfNull(season, nameof(season));
        SeasonUpdated?.Invoke(null, new(series, season, reason));
    }

    public void OnSeasonUpdated(ISeries anime, ISeason season, UpdateReason reason)
    {
        ArgumentNullException.ThrowIfNull(anime, nameof(anime));
        ArgumentNullException.ThrowIfNull(season, nameof(season));
        SeasonUpdated?.Invoke(null, new(anime, season, reason));
    }

    public void OnEpisodeUpdated(IShokoSeries series, IShokoEpisode episode, UpdateReason reason)
    {
        ArgumentNullException.ThrowIfNull(series, nameof(series));
        ArgumentNullException.ThrowIfNull(episode, nameof(episode));
        EpisodeUpdated?.Invoke(null, new(series, episode, reason));
    }

    public void OnEpisodeUpdated(ISeries anime, IEpisode episode, UpdateReason reason)
    {
        ArgumentNullException.ThrowIfNull(anime, nameof(anime));
        ArgumentNullException.ThrowIfNull(episode, nameof(episode));
        EpisodeUpdated?.Invoke(null, new(anime, episode, reason));
    }

    public void OnMovieUpdated(IMovie movie, UpdateReason reason)
    {
        ArgumentNullException.ThrowIfNull(movie, nameof(movie));
        MovieUpdated?.Invoke(null, new(movie, reason));
    }

    public void OnAVDumpMessage(AVDumpEventType messageType, string? message = null)
    {
        AVDumpEvent?.Invoke(null, new(messageType, message));
    }

    public void OnAVDumpInstallException(Exception ex)
    {
        AVDumpEvent?.Invoke(null, new(AVDumpEventType.InstallException, ex));
    }

    public void OnAVDumpStart(AVDumpHelper.AVDumpSession session)
    {
        AVDumpEvent?.Invoke(null, new(AVDumpEventType.Started)
        {
            SessionID = session.SessionID,
            VideoIDs = session.VideoIDs,
            AbsolutePaths = session.AbsolutePaths,
            StartedAt = session.StartedAt,
            Progress = 0,
            SucceededCreqCount = 0,
            FailedCreqCount = 0,
            PendingCreqCount = 0,
        });
    }

    public void OnAVDumpEnd(AVDumpHelper.AVDumpSession session)
    {
        AVDumpEvent?.Invoke(null, new(session.IsSuccess ? AVDumpEventType.Success : AVDumpEventType.Failure)
        {
            SessionID = session.SessionID,
            VideoIDs = session.VideoIDs,
            AbsolutePaths = session.AbsolutePaths,
            Progress = session.Progress,
            SucceededCreqCount = session.IsSuccess ? null : session.SucceededCreqCount,
            FailedCreqCount = session.IsSuccess ? null : session.FailedCreqCount,
            PendingCreqCount = session.IsSuccess ? null : session.PendingCreqCount,
            ED2Ks = session.IsSuccess ? session.ED2Ks.ToList() : null,
            Message = session.StandardOutput,
            ErrorMessage = string.IsNullOrEmpty(session.StandardError) ? null : session.StandardError,
            StartedAt = session.StartedAt,
            EndedAt = session.EndedAt,
        });
    }

    public void OnAVDumpMessage(AVDumpHelper.AVDumpSession session, AVDumpEventType messageType, string? message = null)
    {
        AVDumpEvent?.Invoke(null, new(messageType, message)
        {
            SessionID = session.SessionID,
        });
    }

    public void OnAVDumpProgress(AVDumpHelper.AVDumpSession session, double progress)
    {
        AVDumpEvent?.Invoke(null, new(AVDumpEventType.Progress)
        {
            SessionID = session.SessionID,
            Progress = progress,
        });
    }

    public void OnAVDumpCreqUpdate(AVDumpHelper.AVDumpSession session, int succeeded, int failed, int pending)
    {
        AVDumpEvent?.Invoke(null, new(AVDumpEventType.CreqUpdate)
        {
            SessionID = session.SessionID,
            SucceededCreqCount = succeeded,
            FailedCreqCount = failed,
            PendingCreqCount = pending,
        });
    }

    public void OnAVDumpGenericException(AVDumpHelper.AVDumpSession session, Exception ex)
    {
        AVDumpEvent?.Invoke(null, new(AVDumpEventType.GenericException, ex)
        {
            SessionID = session.SessionID,
            Message = session.StandardOutput,
            StartedAt = session.StartedAt,
            EndedAt = session.EndedAt,
        });
    }

    public void OnStarting()
    {
        Starting?.Invoke(null, EventArgs.Empty);
    }

    public void OnStarted()
    {
        Started?.Invoke(null, EventArgs.Empty);
    }

    public void OnShutdown()
    {
        Shutdown?.Invoke(null, EventArgs.Empty);
    }
}
