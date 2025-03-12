using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server;

public class ShokoEventHandler : IShokoEventHandler
{
    public event EventHandler<FileEventArgs>? FileDeleted;

    public event EventHandler<FileDetectedEventArgs>? FileDetected;

    public event EventHandler<FileRenamedEventArgs>? FileRenamed;

    public event EventHandler<FileMovedEventArgs>? FileMoved;

    public event EventHandler<AniDBBannedEventArgs>? AniDBBanned;

    public event EventHandler<SeriesInfoUpdatedEventArgs>? SeriesUpdated;

    public event EventHandler<EpisodeInfoUpdatedEventArgs>? EpisodeUpdated;

    public event EventHandler<MovieInfoUpdatedEventArgs>? MovieUpdated;

    public event EventHandler<AVDumpEventArgs>? AVDumpEvent;

    public event EventHandler? Starting;

    public event EventHandler? Started;

    public event EventHandler? Shutdown;

    private static ShokoEventHandler? _instance;

    public static ShokoEventHandler Instance => _instance ??= new();

    public void OnFileDetected(IManagedFolder folder, FileInfo file)
    {
        FileDetected?.Invoke(null, new(file.FullName[folder.Path.Length..], file, folder));
    }

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

    public void OnFileMoved(IManagedFolder oldFolder, IManagedFolder newFolder, string oldPath, string newPath, IVideoFile vlp)
    {
        var vl = vlp.Video!;
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
        FileMoved?.Invoke(null, new(newPath, newFolder, oldPath, oldFolder, vlp, vl, episodes, series, groups));
    }

    public void OnFileRenamed(IManagedFolder folder, string oldName, string newName, IVideoFile vlp)
    {
        var path = vlp.RelativePath;
        var vl = vlp.Video!;
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
        FileRenamed?.Invoke(null, new(path, folder, newName, oldName, vlp, vl, episodes, series, groups));
    }

    public void OnAniDBBanned(AniDBBanType type, DateTime time, DateTime resumeTime)
    {
        AniDBBanned?.Invoke(null, new(type, time, resumeTime));
    }

    public void OnSeriesUpdated(SVR_AniDB_Anime anime, UpdateReason reason, IEnumerable<KeyValuePair<SVR_AniDB_Episode, UpdateReason>>? episodes = null)
        => OnSeriesUpdated(anime, reason, episodes?.Select(e => ((IEpisode)e.Key, e.Value)));

    public void OnSeriesUpdated(TMDB_Show show, UpdateReason reason, IEnumerable<KeyValuePair<TMDB_Episode, UpdateReason>>? episodes = null)
        => OnSeriesUpdated(show, reason, episodes?.Select(e => ((IEpisode)e.Key, e.Value)));

    public void OnSeriesUpdated(ISeries series, UpdateReason reason, IEnumerable<(IEpisode episode, UpdateReason reason)>? episodes = null)
    {
        ArgumentNullException.ThrowIfNull(series, nameof(series));
        var episodeEvents = episodes?.Select(e => new EpisodeInfoUpdatedEventArgs(series, e.episode, e.reason)).ToList() ?? [];
        SeriesUpdated?.Invoke(null, new(series, reason, episodeEvents));
        foreach (var e in episodeEvents)
            EpisodeUpdated?.Invoke(null, e);
    }

    public void OnSeriesUpdated(IShokoSeries series, UpdateReason reason, IEnumerable<KeyValuePair<SVR_AnimeEpisode, UpdateReason>> episodes)
        => OnSeriesUpdated(series, reason, episodes.Select(e => ((IShokoEpisode)e.Key, e.Value)));

    public void OnSeriesUpdated(IShokoSeries series, UpdateReason reason, IEnumerable<(IShokoEpisode episode, UpdateReason reason)>? episodes = null)
    {
        ArgumentNullException.ThrowIfNull(series, nameof(series));
        var episodeEvents = episodes?.Select(e => new EpisodeInfoUpdatedEventArgs(series, e.episode, e.reason)).ToList() ?? [];
        SeriesUpdated?.Invoke(null, new(series, reason, episodeEvents));
        foreach (var e in episodeEvents)
            EpisodeUpdated?.Invoke(null, e);
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
