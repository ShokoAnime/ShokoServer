using System;
using System.Collections.Generic;
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
    public event EventHandler<AniDBBannedEventArgs> AniDBBanned;
    public event EventHandler<SeriesInfoUpdatedEventArgs> SeriesUpdated;
    public event EventHandler<EpisodeInfoUpdatedEventArgs> EpisodeUpdated;
    public event EventHandler<SettingsSavedEventArgs> SettingsSaved;

    public event EventHandler Start;
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

    public void OnStart()
    {
        Start?.Invoke(null, EventArgs.Empty);
    }

    public bool OnShutdown()
    {
        var args = new CancelEventArgs();
        Shutdown?.Invoke(null, args);
        return !args.Cancel;
    }
}
