using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;
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

    public event EventHandler<FileEventArgs>? FileHashed;

    public event EventHandler<FileNotMatchedEventArgs>? FileNotMatched;

    public event EventHandler<FileEventArgs>? FileMatched;

    public event EventHandler<FileRenamedEventArgs>? FileRenamed;

    public event EventHandler<FileMovedEventArgs>? FileMoved;

    public event EventHandler<AniDBBannedEventArgs>? AniDBBanned;

    public event EventHandler<SeriesInfoUpdatedEventArgs>? SeriesUpdated;

    public event EventHandler<EpisodeInfoUpdatedEventArgs>? EpisodeUpdated;

    public event EventHandler<MovieInfoUpdatedEventArgs>? MovieUpdated;

    public event EventHandler<SettingsSavedEventArgs>? SettingsSaved;

    public event EventHandler<AVDumpEventArgs>? AVDumpEvent;

    public event EventHandler? Starting;

    public event EventHandler? Started;

    public event EventHandler<CancelEventArgs>? Shutdown;

    private static ShokoEventHandler? _instance;

    public static ShokoEventHandler Instance => _instance ??= new();

    public void OnFileDetected(SVR_ImportFolder folder, FileInfo file)
    {
        FileDetected?.Invoke(null, new(file.FullName[folder.ImportFolderLocation.Length..], file, folder));
    }

    public void OnFileHashed(SVR_ImportFolder folder, SVR_VideoLocal_Place vlp, SVR_VideoLocal vl)
    {
        var relativePath = vlp.FilePath;
        var xrefs = vl.EpisodeCrossReferences;
        var episodes = xrefs
            .Select(x => x.AnimeEpisode)
            .WhereNotNull()
            .ToList();
        var series = xrefs
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.AnimeSeries)
            .WhereNotNull()
            .ToList();
        var groups = series
            .DistinctBy(a => a.AnimeGroupID)
            .Select(a => a.AnimeGroup)
            .WhereNotNull()
            .ToList();
        FileHashed?.Invoke(null, new(relativePath, folder, vlp, vl, episodes, series, groups));
    }

    public void OnFileDeleted(SVR_ImportFolder folder, SVR_VideoLocal_Place vlp, SVR_VideoLocal vl)
    {
        var path = vlp.FilePath;
        var xrefs = vl.EpisodeCrossReferences;
        var episodes = xrefs
            .Select(x => x.AnimeEpisode)
            .WhereNotNull()
            .ToList();
        var series = xrefs
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.AnimeSeries)
            .WhereNotNull()
            .ToList();
        var groups = series
            .DistinctBy(a => a.AnimeGroupID)
            .Select(a => a.AnimeGroup)
            .WhereNotNull()
            .ToList();
        FileDeleted?.Invoke(null, new(path, folder, vlp, vl, episodes, series, groups));
    }

    public void OnFileMatched(SVR_VideoLocal_Place vlp, SVR_VideoLocal vl)
    {
        var path = vlp.FilePath;
        var xrefs = vl.EpisodeCrossReferences;
        var episodes = xrefs
            .Select(x => x.AnimeEpisode)
            .WhereNotNull()
            .ToList();
        var series = xrefs
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.AnimeSeries)
            .WhereNotNull()
            .ToList();
        var groups = series
            .DistinctBy(a => a.AnimeGroupID)
            .Select(a => a.AnimeGroup)
            .WhereNotNull()
            .ToList();
        FileMatched?.Invoke(null, new(path, vlp.ImportFolder!, vlp, vl, episodes, series, groups));
    }

    public void OnFileNotMatched(SVR_VideoLocal_Place vlp, SVR_VideoLocal vl, int autoMatchAttempts, bool hasXRefs, bool isUDPBanned)
    {
        var path = vlp.FilePath;
        var xrefs = vl.EpisodeCrossReferences;
        var episodes = xrefs
            .Select(x => x.AnimeEpisode)
            .WhereNotNull()
            .ToList();
        var series = xrefs
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.AnimeSeries)
            .WhereNotNull()
            .ToList();
        var groups = series
            .DistinctBy(a => a.AnimeGroupID)
            .Select(a => a.AnimeGroup)
            .WhereNotNull()
            .ToList();
        FileNotMatched?.Invoke(null, new(path, vlp.ImportFolder!, vlp, vl, episodes, series, groups, autoMatchAttempts, hasXRefs, isUDPBanned));
    }

    public void OnFileMoved(IImportFolder oldFolder, IImportFolder newFolder, string oldPath, string newPath, SVR_VideoLocal_Place vlp)
    {
        var vl = vlp.VideoLocal!;
        var xrefs = vl.EpisodeCrossReferences;
        var episodes = xrefs
            .Select(x => x.AnimeEpisode)
            .WhereNotNull()
            .ToList();
        var series = xrefs
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.AnimeSeries)
            .WhereNotNull()
            .ToList();
        var groups = series
            .DistinctBy(a => a.AnimeGroupID)
            .Select(a => a.AnimeGroup)
            .WhereNotNull()
            .ToList();
        FileMoved?.Invoke(null, new(newPath, newFolder, oldPath, oldFolder, vlp, vl, episodes, series, groups));
    }

    public void OnFileRenamed(IImportFolder folder, string oldName, string newName, SVR_VideoLocal_Place vlp)
    {
        var path = vlp.FilePath;
        var vl = vlp.VideoLocal!;
        var xrefs = vl.EpisodeCrossReferences;
        var episodes = xrefs
            .Select(x => x.AnimeEpisode)
            .WhereNotNull()
            .ToList();
        var series = xrefs
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.AnimeSeries)
            .WhereNotNull()
            .ToList();
        var groups = series
            .DistinctBy(a => a.AnimeGroupID)
            .Select(a => a.AnimeGroup)
            .WhereNotNull()
            .ToList();
        FileRenamed?.Invoke(null, new(path, folder, newName, oldName, vlp, vl, episodes, series, groups));
    }

    public void OnAniDBBanned(AniDBBanType type, DateTime time, DateTime resumeTime)
    {
        AniDBBanned?.Invoke(null, new(type, time, resumeTime));
    }

    public void OnSeriesUpdated(SVR_AnimeSeries series, UpdateReason reason, IEnumerable<(SVR_AnimeEpisode episode, UpdateReason reason)>? episodes = null)
    {
        ArgumentNullException.ThrowIfNull(series, nameof(series));
        var episodeEvents = episodes?.Select(e => new EpisodeInfoUpdatedEventArgs(series, e.episode, e.reason)).ToList() ?? [];
        SeriesUpdated?.Invoke(null, new(series, reason, episodeEvents));
        foreach (var e in episodeEvents)
            EpisodeUpdated?.Invoke(null, e);
    }

    public void OnSeriesUpdated(SVR_AnimeSeries series, UpdateReason reason, IEnumerable<KeyValuePair<SVR_AnimeEpisode, UpdateReason>> episodes)
    {
        ArgumentNullException.ThrowIfNull(series, nameof(series));
        var episodeEvents = episodes.Select(e => new EpisodeInfoUpdatedEventArgs(series, e.Key, e.Value)).ToList();
        SeriesUpdated?.Invoke(null, new(series, reason, episodeEvents));
        foreach (var e in episodeEvents)
            EpisodeUpdated?.Invoke(null, e);
    }

    public void OnSeriesUpdated(SVR_AniDB_Anime anime, UpdateReason reason, IEnumerable<(SVR_AniDB_Episode episode, UpdateReason reason)>? episodes = null)
    {
        ArgumentNullException.ThrowIfNull(anime, nameof(anime));
        var episodeEvents = episodes?.Select(e => new EpisodeInfoUpdatedEventArgs(anime, e.episode, e.reason)).ToList() ?? [];
        SeriesUpdated?.Invoke(null, new(anime, reason, episodeEvents));
        foreach (var e in episodeEvents)
            EpisodeUpdated?.Invoke(null, e);
    }

    public void OnSeriesUpdated(SVR_AniDB_Anime anime, UpdateReason reason, IEnumerable<KeyValuePair<SVR_AniDB_Episode, UpdateReason>> episodes)
    {
        ArgumentNullException.ThrowIfNull(anime, nameof(anime));
        var episodeEvents = episodes.Select(e => new EpisodeInfoUpdatedEventArgs(anime, e.Key, e.Value)).ToList();
        SeriesUpdated?.Invoke(null, new(anime, reason, episodeEvents));
        foreach (var e in episodeEvents)
            EpisodeUpdated?.Invoke(null, e);
    }

    public void OnSeriesUpdated(TMDB_Show show, UpdateReason reason, IEnumerable<(TMDB_Episode episode, UpdateReason reason)>? episodes = null)
    {
        ArgumentNullException.ThrowIfNull(show, nameof(show));
        var episodeEvents = episodes?.Select(e => new EpisodeInfoUpdatedEventArgs(show, e.episode, e.reason)).ToList() ?? [];
        SeriesUpdated?.Invoke(null, new(show, reason, episodeEvents));
        foreach (var e in episodeEvents)
            EpisodeUpdated?.Invoke(null, e);
    }

    public void OnSeriesUpdated(TMDB_Show show, UpdateReason reason, IEnumerable<KeyValuePair<TMDB_Episode, UpdateReason>> episodes)
    {
        ArgumentNullException.ThrowIfNull(show, nameof(show));
        var episodeEvents = episodes.Select(e => new EpisodeInfoUpdatedEventArgs(show, e.Key, e.Value)).ToList();
        SeriesUpdated?.Invoke(null, new(show, reason, episodeEvents));
        foreach (var e in episodeEvents)
            EpisodeUpdated?.Invoke(null, e);
    }

    public void OnEpisodeUpdated(SVR_AnimeSeries series, SVR_AnimeEpisode episode, UpdateReason reason)
    {
        ArgumentNullException.ThrowIfNull(series, nameof(series));
        ArgumentNullException.ThrowIfNull(episode, nameof(episode));
        EpisodeUpdated?.Invoke(null, new(series, episode, reason));
    }

    public void OnEpisodeUpdated(SVR_AniDB_Anime anime, SVR_AniDB_Episode episode, UpdateReason reason)
    {
        ArgumentNullException.ThrowIfNull(anime, nameof(anime));
        ArgumentNullException.ThrowIfNull(episode, nameof(episode));
        EpisodeUpdated?.Invoke(null, new(anime, episode, reason));
    }

    public void OnEpisodeUpdated(TMDB_Show show, TMDB_Episode episode, UpdateReason reason)
    {
        ArgumentNullException.ThrowIfNull(show, nameof(show));
        ArgumentNullException.ThrowIfNull(episode, nameof(episode));
        EpisodeUpdated?.Invoke(null, new(show, episode, reason));
    }

    public void OnMovieUpdated(TMDB_Movie movie, UpdateReason reason)
    {
        ArgumentNullException.ThrowIfNull(movie, nameof(movie));
        MovieUpdated?.Invoke(null, new(movie, reason));
    }


    public void OnSettingsSaved()
    {
        SettingsSaved?.Invoke(null, new SettingsSavedEventArgs());
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

    public bool OnShutdown()
    {
        var args = new CancelEventArgs();
        Shutdown?.Invoke(null, args);
        return !args.Cancel;
    }
}
