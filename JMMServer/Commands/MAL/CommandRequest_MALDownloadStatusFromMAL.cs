using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.WebCache;
using JMMServer.Providers.MyAnimeList;
using AniDBAPI;
using System.Xml;
using JMMDatabase;
using JMMDatabase.Extensions;
using JMMModels;
using JMMModels.Childs;
using JMMModels.Extensions;
using JMMServer.Entities;
using JMMServer.Repositories;
using JMMServerModels.DB.Childs;
using AniDB_Episode = JMMServer.Entities.AniDB_Episode;
using AnimeEpisode = JMMServer.Entities.AnimeEpisode;

namespace JMMServer.Commands.MAL
{
	[Serializable]
	public class CommandRequest_MALDownloadStatusFromMAL : BaseCommandRequest, ICommandRequest
	{
		public CommandRequestPriority DefaultPriority 
		{
			get { return CommandRequestPriority.Priority9; }
		}

		public string PrettyDescription
		{
			get
			{
				return "Downloading watched states from MAL";
			}
		}


		public CommandRequest_MALDownloadStatusFromMAL(string userid)
		{
		    this.JMMUserId = userid;
            this.CommandType = CommandRequestType.MAL_DownloadWatchedStates;
			this.Priority = DefaultPriority;
            this.Id= "CommandRequest_MALDownloadStatusFromMAL";
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_MALDownloadStatusFromMAL");

			try
			{
			    JMMModels.JMMUser oruser = Store.JmmUserRepo.Find(JMMUserId).GetRealUser();
                JMMModels.JMMUser usermal = oruser.GetUserWithAuth(AuthorizationProvider.MAL);
			    JMMModels.JMMUser useranidb = oruser.GetUserWithAuth(AuthorizationProvider.AniDB);
                if (usermal==null || useranidb==null || usermal!=useranidb)
                    return;


				// find the latest eps to update
				AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();

				myanimelist mal = MALHelper.GetMALAnimeList(usermal.Id);
				if (mal == null) return;
				if (mal.anime == null) return;

				foreach (myanimelistAnime malAnime in mal.anime)
				{
                    // look up the anime
                    List<JMMModels.AnimeSerie> sers = Store.AnimeSerieRepo.AnimeSeriesFromMAL(malAnime.series_animedb_id.ToString());
                    if (malAnime.series_animedb_id == 8107 || malAnime.series_animedb_id == 10737)
                    {
                        Console.Write("");
                    }
                    if (sers.Count == 0)
				        continue;
				    foreach (AnimeSerie ser in sers)
				    {
				        AniDB_Anime_MAL first = ser.AniDB_Anime.MALs.First(a => a.MalId == malAnime.series_animedb_id.ToString());
				        int maxep =
				            ser.AniDB_Anime.MALs.Where(a => a.StartEpisodeType == first.StartEpisodeType)
				                .Max(a => a.StartEpisodeNumber);
				        if (maxep > first.StartEpisodeNumber)
				            maxep = maxep - 1;
				        else
				            maxep = int.MaxValue;
				        foreach (JMMModels.Childs.AnimeEpisode ep in ser.Episodes.Where(a => a.AniDbEpisodes.Any(b => b.Value.Any(a => a.Type == first.StartEpisodeType))))				            
				        {
				            int adjustedWatchedEps = malAnime.my_watched_episodes + first.StartEpisodeNumber - 1;
				            int epNum = ep.GetAniDB_Episode().Number;
                            
				            if (epNum < first.StartEpisodeNumber || epNum > maxep) continue;

				            //AnimeEpisode_User usrRec = ep.GetUserRecord(user.JMMUserID);
				            UserStats stats = ep.UsersStats.FirstOrDefault(a => a.JMMUserId == usermal.Id);
				            if (epNum <= adjustedWatchedEps)
				            {

				                // update if the user doesn't have a record (means not watched)
				                // or it is currently un-watched
				                bool update = false;
				                if (stats == null)
				                    update = true;
				                else
				                {
				                    if (!stats.WatchedDate.HasValue) update = true;
				                }

				                if (update) ep.ToggleWatchedStatus(true, true, DateTime.Now, usermal, false);
				            }
				            else
				            {
				                if (stats?.WatchedDate != null) ep.ToggleWatchedStatus(false, true, DateTime.Now, usermal, false);
				            }
				        }
				    }
				}
			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_MALDownloadStatusFromMAL: {0}", ex.ToString());
			}
		}

	}
}
