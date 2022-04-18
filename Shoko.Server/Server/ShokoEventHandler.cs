using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Models;

namespace Shoko.Server
{
    public class ShokoEventHandler : IShokoEventHandler
    {
        public event EventHandler<FileDetectedEventArgs> FileDetected;
        public event EventHandler<FileHashedEventArgs> FileHashed;
        public event EventHandler<FileMatchedEventArgs> FileMatched;
        public event EventHandler<AniDBBannedEventArgs> AniDBBanned;
        public event EventHandler<SeriesInfoUpdatedEventArgs> SeriesUpdated;
        public event EventHandler<EpisodeInfoUpdatedEventArgs> EpisodeUpdated;

        private static ShokoEventHandler _instance;
        public static ShokoEventHandler Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = new ShokoEventHandler();
                return _instance;
            }
        }

        internal void OnFileDetected(SVR_ImportFolder folder, FileInfo file)
        {
            var path = file.FullName.Replace(folder.ImportFolderLocation, "");
            if (!path.StartsWith("/")) path = "/" + path;
            FileDetected?.Invoke(null, new FileDetectedEventArgs
            {
                FileInfo = file,
                ImportFolder = folder,
                RelativePath = path,
            });
        }
        
        internal void OnFileHashed(SVR_ImportFolder folder, SVR_VideoLocal_Place vlp)
        {
            var path = vlp.FilePath;
            FileHashed?.Invoke(null, new FileHashedEventArgs
            {
                FileInfo = vlp.GetFile(),
                ImportFolder = folder,
                RelativePath = path,
                Hashes = vlp.Hashes,
                MediaInfo = vlp.MediaInfo,
            });
        }

        internal void OnFileMatched(SVR_VideoLocal_Place vlp)
        {
            var series = vlp.VideoLocal?.GetAnimeEpisodes().Select(a => a.GetAnimeSeries()).DistinctBy(a => a.AniDB_ID).ToList() ?? new List<SVR_AnimeSeries>();
            FileMatched?.Invoke(
                null, new FileMatchedEventArgs
                {
                    FileInfo = vlp,
                    AnimeInfo = series.Select(a => a.GetAnime()).Cast<IAnime>().ToList(),
                    EpisodeInfo = vlp.VideoLocal?.GetAnimeEpisodes().Cast<IEpisode>().ToList(),
                    GroupInfo = series.Select(a => a.AnimeGroup).DistinctBy(a => a.AnimeGroupID).Cast<IGroup>().ToList(),
                }
            );
        }

        internal void OnAniDBBanned(AniDBBanType type, DateTime time, DateTime resumeTime)
        {
            AniDBBanned?.Invoke(null, new AniDBBannedEventArgs
            {
                Type = type,
                Time = time,
                ResumeTime = resumeTime,
            });
        }

        internal void OnSeriesUpdated(DataSourceEnum source, SVR_AniDB_Anime anime)
        {
            SeriesUpdated?.Invoke(null, new SeriesInfoUpdatedEventArgs
            {
                Type = source,
                AnimeInfo = anime,
            });
        }

        internal void OnEpisodeUpdated(DataSourceEnum source, SVR_AniDB_Anime anime, SVR_AnimeEpisode episode)
        {
            EpisodeUpdated?.Invoke(null, new EpisodeInfoUpdatedEventArgs
            {
                Type = source,
                AnimeInfo = anime,
                EpisodeInfo = episode,
            });
        }
    }
}
