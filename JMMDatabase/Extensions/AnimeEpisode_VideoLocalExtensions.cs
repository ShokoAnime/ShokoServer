using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMModels;
using JMMModels.Childs;
using JMMServer;
using JMMServer.Commands;
using JMMServer.Commands.MAL;
using JMMServer.Entities;
using JMMServer.Repositories;
using AniDB_Episode = JMMModels.AniDB_Episode;
using AniDB_File = JMMModels.AniDB_File;
using AnimeEpisode = JMMModels.Childs.AnimeEpisode;
using ImportFolder = JMMModels.ImportFolder;
using JMMUser = JMMModels.JMMUser;
using VideoLocal = JMMModels.VideoLocal;

namespace JMMDatabase.Extensions
{
    public static class AnimeEpisode_VideoLocalExtensions
    {
        public static string ToStringDetailed(this VideoLocal vl)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Environment.NewLine);
            sb.Append("FilePath: " + vl.FileInfo.Path);
            sb.Append(Environment.NewLine);
            sb.Append("ImportFolderID: " + vl.FileInfo.ImportFolderId);
            sb.Append(Environment.NewLine);
            sb.Append("Hash: " + vl.Hash);
            sb.Append(Environment.NewLine);
            sb.Append("FileSize: " + vl.FileSize);
            sb.Append(Environment.NewLine);
            try
            {
                ImportFolder fl = vl.ImportFolder();
                if (fl != null)
                    sb.Append("ImportFolderLocation: " + fl.Location);
            }
            catch (Exception ex)
            {
                sb.Append("ImportFolderLocation: " + ex);
            }
            sb.Append(Environment.NewLine);
            return sb.ToString();
        }

        public static string ToString(this VideoLocal vl)
        {
            return $"{vl.FullServerPath()} --- {vl.Hash}";
        }
    
        public static ImportFolder ImportFolder(this VideoLocal vl)
        {
            return Store.ImportFolderRepo.Find(vl.FileInfo.ImportFolderId);
        }

        public static string FullServerPath(this VideoLocal vl)
        {
            return Path.Combine(vl.ImportFolder().Location, vl.FileInfo.Path);
        }



        public static void ToggleWatchedStatus(this VideoLocal vl, AnimeSerie ser, bool watched, JMMUser user)
        {
            ToggleWatchedStatus(vl, ser, watched, true, null,user, true, true);
        }
        
        public static void ToggleWatchedStatus(this VideoLocal vl, AnimeSerie serie, bool watched, bool updateOnline, DateTime? watchedDate, JMMUser user, bool syncTrakt, bool updateWatchedDate)
        {
            if (!user.IsRealUserAccount)
            {
                user = user.GetAniDBUser();
                if (user == null)
                    return;
            }
            vl.UsersStats.SetWatchedState(watched,user.Id, watchedDate, updateWatchedDate);
            List<AniDB_Episode> eps = serie.AniDB_EpisodesFromVideoLocal(vl);



            foreach (AniDB_Episode e in eps)
            {
                AniDB_File f = e.Files.FirstOrDefault(a => a.Hash == vl.Hash);
                f?.UserStats.SetWatchedState(watched, user.Id, watchedDate, updateWatchedDate);
                if (updateOnline && user.HasAniDBAccount())
                {
                    if ((watched && ServerSettings.AniDB_MyList_SetWatched) || (!watched && ServerSettings.AniDB_MyList_SetUnwatched))
                    {
                        //TODO ADD user account to CommandRequest (LEO?). If JmmUser isMasterAccount, then use normal login, if not login, setwatchstatus, logout
                        CommandRequest_UpdateMyListFileStatus cmd = new CommandRequest_UpdateMyListFileStatus(vl.Hash, watched, false, watchedDate.HasValue ? Utils.GetAniDBDateAsSeconds(watchedDate) : 0);
                        cmd.Save();
                    }
                }
            }

           
            // now find all the episode records associated with this video file
            // but we also need to check if theer are any other files attached to this episode with a watched
            // status, 

            foreach (AniDB_Episode e in eps)
            {
                AnimeEpisode aep = serie.AnimeEpisodeFromAniDB_Episode(e);
                if (watched)
                {
                    KeyValuePair<int, List<AniDB_Episode>> m = aep.AniDbEpisodes.FirstOrDefault(a => a.Value.Contains(e));
                    float epPercentWatched = 0;
                    foreach (AniDB_Episode k in m.Value)
                    {
                        if (k == e)
                            epPercentWatched += k.Percentage;
                        else
                        {
                            VideoLocal mn =
                                k.VideoLocals.FirstOrDefault(
                                    a => a.UsersStats.Any(b => b.JMMUserId == user.Id && b.WatchedCount > 0));
                            if (mn != null)
                                epPercentWatched += k.Percentage;
                        }
                    }
                    if (epPercentWatched > 95)
                    {
                        aep.UsersStats.SetWatchedState(true, user.Id, watchedDate, updateWatchedDate);
                    }
                    if (syncTrakt && ServerSettings.Trakt_IsEnabled &&
                        !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
                    {
                        CommandRequest_TraktHistoryEpisode cmdSyncTrakt =
                            new CommandRequest_TraktHistoryEpisode(aep, TraktSyncAction.Add);
                        cmdSyncTrakt.Save();
                    }

                    if (!string.IsNullOrEmpty(ServerSettings.MAL_Username) &&
                        !string.IsNullOrEmpty(ServerSettings.MAL_Password))
                    {
                        CommandRequest_MALUpdatedWatchedStatus cmdMAL =
                            new CommandRequest_MALUpdatedWatchedStatus(serie.AniDB_Anime.Id);
                        cmdMAL.Save();
                    }
                }
                else
                {
                    // if setting a file to unwatched only set the episode unwatched, if ALL the files are unwatched
                    if (
                        aep.AniDbEpisodes.SelectMany(a => a.Value)
                            .SelectMany(a => a.VideoLocals)
                            .All(a => a.UsersStats.All(b => b.JMMUserId == user.Id && b.WatchedCount == 0)))
                    {
                        aep.UsersStats.SetWatchedState(false, user.Id, watchedDate, true);
                    }
                    if (syncTrakt && ServerSettings.Trakt_IsEnabled &&
                        !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
                    {
                        CommandRequest_TraktHistoryEpisode cmdSyncTrakt =
                            new CommandRequest_TraktHistoryEpisode(eep.AnimeEpisodeID, TraktSyncAction.Remove);
                        cmdSyncTrakt.Save();
                    }
                    if (!string.IsNullOrEmpty(ServerSettings.MAL_Username) &&
                        !string.IsNullOrEmpty(ServerSettings.MAL_Password))
                    {
                        CommandRequest_MALUpdatedWatchedStatus cmdMAL =
                            new CommandRequest_MALUpdatedWatchedStatus(serie.AniDB_Anime.Id);
                        cmdMAL.Save();
                    }


                }

            }
        }

    }
}
