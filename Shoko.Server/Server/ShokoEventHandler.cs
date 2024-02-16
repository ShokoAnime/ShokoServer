using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Models;

namespace Shoko.Server;

public class ShokoEventHandler : IShokoEventHandler
{
    public event EventHandler<FileDeletedEventArgs> FileDeleted;
    public event EventHandler<FileDetectedEventArgs> FileDetected;
    public event EventHandler<FileHashedEventArgs> FileHashed;
    public event EventHandler<FileNotMatchedEventArgs> FileNotMatched;
    public event EventHandler<FileMatchedEventArgs> FileMatched;
    public event EventHandler<FileRenamedEventArgs> FileRenamed;
    public event EventHandler<FileMovedEventArgs> FileMoved;
    public event EventHandler<AniDBBannedEventArgs> AniDBBanned;
    public event EventHandler<SeriesInfoUpdatedEventArgs> SeriesUpdated;
    public event EventHandler<EpisodeInfoUpdatedEventArgs> EpisodeUpdated;
    public event EventHandler<SettingsSavedEventArgs> SettingsSaved;
    public event EventHandler<AVDumpEventArgs> AVDumpEvent;

    public event EventHandler Starting;
    public event EventHandler Started;
    public event EventHandler<CancelEventArgs> Shutdown;


    private static ShokoEventHandler _instance;

    public static ShokoEventHandler Instance
    {
        get
        {
            if (_instance != null)
            {
                return _instance;
            }

            _instance = new ShokoEventHandler();
            return _instance;
        }
    }

    public void OnFileDetected(SVR_ImportFolder folder, FileInfo file)
    {
        var path = file.FullName.Replace(folder.ImportFolderLocation, "");
        if (!path.StartsWith("/"))
        {
            path = "/" + path;
        }

        FileDetected?.Invoke(null,
            new FileDetectedEventArgs { FileInfo = file, ImportFolder = folder, RelativePath = path });
    }

    public void OnFileHashed(SVR_ImportFolder folder, SVR_VideoLocal_Place vlp)
    {
        var path = vlp.FilePath;
        FileHashed?.Invoke(null,
            new FileHashedEventArgs { FileInfo = vlp, ImportFolder = folder, RelativePath = path });
    }

    public void OnFileDeleted(SVR_ImportFolder folder, SVR_VideoLocal_Place vlp)
    {
        var path = vlp.FilePath;
        FileDeleted?.Invoke(null,
            new FileDeletedEventArgs { FileInfo = vlp, ImportFolder = folder, RelativePath = path });
    }

    public void OnFileMatched(SVR_VideoLocal_Place vlp, SVR_VideoLocal vl)
    {
        var episodes = vl.GetAnimeEpisodes()
            .ToList();
        var series = episodes
            .DistinctBy(e => e.AnimeSeriesID)
            .Select(e => e.GetAnimeSeries())
            .ToList();
        FileMatched?.Invoke(
            null,
            new FileMatchedEventArgs
            {
                RelativePath = vlp.FilePath,
                FileInfo = vlp,
                ImportFolder = vlp.ImportFolder,
                AnimeInfo = series.Select(a => a.GetAnime()).Cast<IAnime>().ToList(),
                EpisodeInfo = episodes.Cast<IEpisode>().ToList(),
                GroupInfo = series.DistinctBy(a => a.AnimeGroupID).Select(a => a.AnimeGroup).Cast<IGroup>().ToList()
            }
        );
    }

    public void OnFileNotMatched(SVR_VideoLocal_Place vlp, SVR_VideoLocal vl, int autoMatchAttempts, bool hasXRefs, bool isUDPBanned)
    {
        FileNotMatched?.Invoke(
            null,
            new FileNotMatchedEventArgs
            {
                RelativePath = vlp.FilePath,
                FileInfo = vlp,
                ImportFolder = vlp.ImportFolder,
                AutoMatchAttempts = autoMatchAttempts,
                HasCrossReferences = hasXRefs,
                IsUDPBanned = isUDPBanned,
            }
        );
    }

    public void OnFileMoved(SVR_ImportFolder oldFolder, SVR_ImportFolder newFolder, string oldPath, string newPath, SVR_VideoLocal_Place vlp)
    {
        FileMoved?.Invoke(null,
            new FileMovedEventArgs { FileInfo = vlp, NewImportFolder = newFolder, OldImportFolder = oldFolder, NewRelativePath = newPath, OldRelativePath = oldPath});
    }

    public void OnFileRenamed(SVR_ImportFolder folder, string oldName, string newName, SVR_VideoLocal_Place vlp)
    {
        FileRenamed?.Invoke(null,
            new FileRenamedEventArgs { FileInfo = vlp, ImportFolder = folder, OldFileName = oldName, NewFileName = newName, RelativePath = vlp.FilePath});
    }

    public void OnAniDBBanned(AniDBBanType type, DateTime time, DateTime resumeTime)
    {
        AniDBBanned?.Invoke(null, new AniDBBannedEventArgs { Type = type, Time = time, ResumeTime = resumeTime });
    }

    public void OnSeriesUpdated(DataSourceEnum source, SVR_AniDB_Anime anime)
    {
        SeriesUpdated?.Invoke(null, new SeriesInfoUpdatedEventArgs { Type = source, AnimeInfo = anime });
    }

    public void OnEpisodeUpdated(DataSourceEnum source, SVR_AniDB_Anime anime, SVR_AnimeEpisode episode)
    {
        EpisodeUpdated?.Invoke(null,
            new EpisodeInfoUpdatedEventArgs { Type = source, AnimeInfo = anime, EpisodeInfo = episode });
    }

    public void OnSettingsSaved()
    {
        SettingsSaved?.Invoke(null, new SettingsSavedEventArgs());
    }

    public void OnAVDumpMessage(AVDumpEventType messageType, string message = null)
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
            CommandID = session.CommandID,
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
            CommandID = session.CommandID,
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

    public void OnAVDumpMessage(AVDumpHelper.AVDumpSession session, AVDumpEventType messageType, string message = null)
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
