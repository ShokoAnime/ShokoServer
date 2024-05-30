using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Models;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server;

public class ShokoEventHandler : IShokoEventHandler
{
    public event EventHandler<FileDeletedEventArgs>? FileDeleted;

    public event EventHandler<FileDetectedEventArgs>? FileDetected;

    public event EventHandler<FileHashedEventArgs>? FileHashed;

    public event EventHandler<FileNotMatchedEventArgs>? FileNotMatched;

    public event EventHandler<FileMatchedEventArgs>? FileMatched;

    public event EventHandler<FileRenamedEventArgs>? FileRenamed;

    public event EventHandler<FileMovedEventArgs>? FileMoved;

    public event EventHandler<AniDBBannedEventArgs>? AniDBBanned;

    public event EventHandler<SeriesInfoUpdatedEventArgs>? SeriesUpdated;

    public event EventHandler<EpisodeInfoUpdatedEventArgs>? EpisodeUpdated;

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
        var xrefs = vl.EpisodeCrossRefs;
        var episodes = xrefs
            .Select(x => x.GetEpisode())
            .WhereNotNull()
            .ToList();
        var series = xrefs
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.GetAnime())
            .WhereNotNull()
            .ToList();
        var episodeInfo = episodes.Cast<IEpisode>().ToList();
        var animeInfo = series.Cast<IAnime>().ToList();
        var groupInfo = xrefs
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.GetAnimeSeries())
            .WhereNotNull()
            .DistinctBy(a => a.AnimeGroupID)
            .Select(a => a.AnimeGroup)
            .WhereNotNull()
            .Cast<IGroup>()
            .ToList();
        FileHashed?.Invoke(null, new(relativePath, folder, vlp, vl, episodeInfo, animeInfo, groupInfo));
    }

    public void OnFileDeleted(SVR_ImportFolder folder, SVR_VideoLocal_Place vlp, SVR_VideoLocal vl)
    {
        var path = vlp.FilePath;
        var xrefs = vl.EpisodeCrossRefs;
        var episodes = xrefs
            .Select(x => x.GetEpisode())
            .WhereNotNull()
            .ToList();
        var series = xrefs
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.GetAnime())
            .WhereNotNull()
            .ToList();
        var episodeInfo = episodes.Cast<IEpisode>().ToList();
        var animeInfo = series.Cast<IAnime>().ToList();
        var groupInfo = xrefs
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.GetAnimeSeries())
            .WhereNotNull()
            .DistinctBy(a => a.AnimeGroupID)
            .Select(a => a.AnimeGroup)
            .WhereNotNull()
            .Cast<IGroup>()
            .ToList();
        FileDeleted?.Invoke(null, new(path, folder, vlp, vl, episodeInfo, animeInfo, groupInfo));
    }

    public void OnFileMatched(SVR_VideoLocal_Place vlp, SVR_VideoLocal vl)
    {
        var path = vlp.FilePath;
        var xrefs = vl.EpisodeCrossRefs;
        var episodes = xrefs
            .Select(x => x.GetEpisode())
            .WhereNotNull()
            .ToList();
        var series = xrefs
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.GetAnime())
            .WhereNotNull()
            .ToList();
        var episodeInfo = episodes.Cast<IEpisode>().ToList();
        var animeInfo = series.Cast<IAnime>().ToList();
        var groupInfo = xrefs
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.GetAnimeSeries())
            .WhereNotNull()
            .DistinctBy(a => a.AnimeGroupID)
            .Select(a => a.AnimeGroup)
            .WhereNotNull()
            .Cast<IGroup>()
            .ToList();
        FileMatched?.Invoke(null, new(path, vlp.ImportFolder, vlp, vl, episodeInfo, animeInfo, groupInfo));
    }

    public void OnFileNotMatched(SVR_VideoLocal_Place vlp, SVR_VideoLocal vl, int autoMatchAttempts, bool hasXRefs, bool isUDPBanned)
    {
        var path = vlp.FilePath;
        var xrefs = vl.EpisodeCrossRefs;
        var episodes = xrefs
            .Select(x => x.GetEpisode())
            .WhereNotNull()
            .ToList();
        var series = xrefs
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.GetAnime())
            .WhereNotNull()
            .ToList();
        var episodeInfo = episodes.Cast<IEpisode>().ToList();
        var animeInfo = series.Cast<IAnime>().ToList();
        var groupInfo = xrefs
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.GetAnimeSeries())
            .WhereNotNull()
            .DistinctBy(a => a.AnimeGroupID)
            .Select(a => a.AnimeGroup)
            .WhereNotNull()
            .Cast<IGroup>()
            .ToList();
        FileNotMatched?.Invoke(null, new(path, vlp.ImportFolder, vlp, vl, episodeInfo, animeInfo, groupInfo, autoMatchAttempts, hasXRefs, isUDPBanned));
    }

    public void OnFileMoved(SVR_ImportFolder oldFolder, SVR_ImportFolder newFolder, string oldPath, string newPath, SVR_VideoLocal_Place vlp)
    {
        var vl = vlp.VideoLocal;
        var xrefs = vl.EpisodeCrossRefs;
        var episodes = xrefs
            .Select(x => x.GetEpisode())
            .WhereNotNull()
            .ToList();
        var series = xrefs
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.GetAnime())
            .WhereNotNull()
            .ToList();
        var episodeInfo = episodes.Cast<IEpisode>().ToList();
        var animeInfo = series.Cast<IAnime>().ToList();
        var groupInfo = xrefs
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.GetAnimeSeries())
            .WhereNotNull()
            .DistinctBy(a => a.AnimeGroupID)
            .Select(a => a.AnimeGroup)
            .WhereNotNull()
            .Cast<IGroup>()
            .ToList();
        FileMoved?.Invoke(null, new(newPath, newFolder, oldPath, oldFolder, vlp, vl, episodeInfo, animeInfo, groupInfo));
    }

    public void OnFileRenamed(SVR_ImportFolder folder, string oldName, string newName, SVR_VideoLocal_Place vlp)
    {
        var path = vlp.FilePath;
        var vl = vlp.VideoLocal;
        var xrefs = vl.EpisodeCrossRefs;
        var episodes = xrefs
            .Select(x => x.GetEpisode())
            .WhereNotNull()
            .ToList();
        var series = xrefs
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.GetAnime())
            .WhereNotNull()
            .ToList();
        var episodeInfo = episodes.Cast<IEpisode>().ToList();
        var animeInfo = series.Cast<IAnime>().ToList();
        var groupInfo = xrefs
            .DistinctBy(x => x.AnimeID)
            .Select(x => x.GetAnimeSeries())
            .WhereNotNull()
            .DistinctBy(a => a.AnimeGroupID)
            .Select(a => a.AnimeGroup)
            .WhereNotNull()
            .Cast<IGroup>()
            .ToList();
        FileRenamed?.Invoke(null, new(path, folder, newName, oldName, vlp, vl, episodeInfo, animeInfo, groupInfo));
    }

    public void OnAniDBBanned(AniDBBanType type, DateTime time, DateTime resumeTime)
    {
        AniDBBanned?.Invoke(null, new(type, time, resumeTime));
    }

    public void OnSeriesUpdated(SVR_AnimeSeries series, UpdateReason reason)
    {
        ArgumentNullException.ThrowIfNull(series, nameof(series));
        SeriesUpdated?.Invoke(null, new(series, reason));
    }

    public void OnSeriesUpdated(SVR_AniDB_Anime anime, UpdateReason reason)
    {
        ArgumentNullException.ThrowIfNull(anime, nameof(anime));
        SeriesUpdated?.Invoke(null, new(anime, reason));
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
