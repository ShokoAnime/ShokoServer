using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Repositories;
using JMMServer.Entities;
using System.Xml;
using AniDBAPI;
using JMMDatabase;
using JMMDatabase.Extensions;
using JMMModels;
using JMMModels.Childs;
using JMMServer.Commands.MAL;
using AnimeEpisode = JMMServer.Entities.AnimeEpisode;
using JMMServerModels.DB;
using JMMServerModels.DB.Childs;
using CommandRequest = JMMServerModels.DB.CommandRequest;
using JMMUser = JMMServer.Entities.JMMUser;
using VideoLocal = JMMModels.VideoLocal;

namespace JMMServer.Commands
{
	[Serializable]
	public class CommandRequest_AddFileToMyList : BaseCommandRequest, ICommandRequest
	{
		public string HashAndSize { get; set; }


	    private List<VideoLocal> vidLocals = null;

		public CommandRequestPriority DefaultPriority
		{
			get { return CommandRequestPriority.Priority9; }
		}

		public string PrettyDescription
		{
			get
			{
			    JMMModels.JMMUser u = Store.JmmUserRepo.Find(JMMUserId);

                if ((vidLocals.HasItems()))
					return $"Adding file to user {u.UserName} MyList: {vidLocals[0].FullServerPath()}";
				return $"Adding file to user {u.UserName} MyList: {HashAndSize}";
			}
		}

		public CommandRequest_AddFileToMyList()
		{
		}

		public CommandRequest_AddFileToMyList(string userid, string hashandsize)
		{
		    HashAndSize = hashandsize;
		    JMMUserId = userid;
			CommandType = CommandRequestType.AniDB_AddFileUDP;
			Priority = DefaultPriority;
            Id= $"CommandRequest_AddFileToMyList_{JMMUserId}_{HashAndSize}";
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_AddFileToMyList: {0}", HashAndSize);
			try
			{
                JMMModels.JMMUser user = Store.JmmUserRepo.Find(JMMUserId).GetRealUser();
			    Dictionary<AnimeSerie, List<VideoLocal>> series = Store.AnimeSerieRepo.AnimeSerieFromVideoLocal(HashAndSize);
                vidLocals = series.SelectMany(a=>a.Value).ToList();
			    if (vidLocals.HasItems())
			    {
			        foreach (AnimeSerie s in series.Keys)
			        {
			            foreach (VideoLocal vl in series[s])
			            {
                            DateTime? watchedDate = null;
                            List<JMMModels.AniDB_Episode> eps = s.AniDB_EpisodesFromVideoLocal(vl);
                            bool isManualLink = vl.CrossRefSource != CrossRefSourceType.AniDB;
			                foreach (JMMModels.AniDB_Episode e in eps)
			                {
                                JMMModels.Childs.AnimeEpisode aep = s.AnimeEpisodeFromAniDB_Episode(e);
                                bool newWatchedStatus = false;
                                JMMModels.JMMUser anidbuser = user.GetAniDBUser();
			                    if (isManualLink)
			                        newWatchedStatus = JMMService.AnidbProcessor.AddFileToMyList(anidbuser.Id, s.AniDB_Anime.Id, e.Number, ref watchedDate);
			                    else
			                        newWatchedStatus = JMMService.AnidbProcessor.AddFileToMyList(anidbuser.Id, new Hash { FileSize = vl.FileSize, ED2KHash = vl.Hash, Info= vl.FileInfo.Path ?? string.Empty }, ref watchedDate);
                                vl.ToggleWatchedStatus(s, newWatchedStatus, false, watchedDate, user, false,true);
                                logger.Info("Adding file to list: {0} - {1}", vl.ToString(), watchedDate);
			                    if (ServerSettings.Import_UseExistingFileWatchedStatus && !newWatchedStatus)
			                    {
                                    UserStats n = aep.UsersStats.FirstOrDefault(a => a.JMMUserId == user.Id);
			                        if ((n != null) && (n.WatchedCount > 0))
			                        {
                                        logger.Info("Setting file as watched, because episode was already watched: {0} - user: {1}", vl.ToString(), user.UserName);                                        
                                        vl.ToggleWatchedStatus(s, true, true, n.WatchedDate, user, false, true);
			                        }
			                    }
                                if (ServerSettings.Trakt_IsEnabled && !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
                                {
                                    CommandRequest_TraktCollectionEpisode cmdSyncTrakt = new CommandRequest_TraktCollectionEpisode(aep.Id, TraktSyncAction.Add);
                                    cmdSyncTrakt.Save();
                                }
                            }
                        }
                        if (!string.IsNullOrEmpty(ServerSettings.MAL_Username) && !string.IsNullOrEmpty(ServerSettings.MAL_Password))
                        {
                            CommandRequest_MALUpdatedWatchedStatus cmdMAL = new CommandRequest_MALUpdatedWatchedStatus(s.AniDB_Anime.Id);
                            cmdMAL.Save();
                        }
                    }
                }
			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_AddFileToMyList: {0} - {1}", Hash, ex.ToString());
				return;
			}
		}
	}
}
