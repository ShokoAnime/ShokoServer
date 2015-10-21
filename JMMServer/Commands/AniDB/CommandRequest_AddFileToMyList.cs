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

		public CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority9;

	    public string PrettyDescription
		{
			get
			{
			    JMMModels.JMMUser u = Store.JmmUserRepo.Find(JMMUserId);
                VideoLocal vl = Store.VideoLocalRepo.Find(HashAndSize);
			    if ((vl != null) && (u!=null))
                    return $"Adding file '{vl.FileInfo.Path}'to user {u.UserName}";
			    return string.Empty;
			}
		}

		public CommandRequest_AddFileToMyList()
		{
		}

		public CommandRequest_AddFileToMyList(string userid, string hashandsize)
		{
		    HashAndSize = hashandsize;
            JMMModels.JMMUser user = Store.JmmUserRepo.Find(userid).GetUserWithAuth(AuthorizationProvider.AniDB);
		    if (user != null)
		        userid = user.Id;
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
		        JMMModels.JMMUser user = Store.JmmUserRepo.Find(JMMUserId);
		        if (user == null)
		            return;
                VideoLocal vl = Store.VideoLocalRepo.Find(HashAndSize);
		        if (vl != null)
		        {
		            foreach (AnimeSerie s in Store.AnimeSerieRepo.AnimeSeriesFromVideoLocal(vl.Id))
		            {
		                DateTime? watchedDate = null;
		                List<JMMModels.AniDB_Episode> eps = s.AniDB_EpisodesFromVideoLocal(vl);
		                bool isManualLink = vl.CrossRefSource != CrossRefSourceType.AniDB;
		                foreach (JMMModels.AniDB_Episode e in eps)
		                {
		                    JMMModels.Childs.AnimeEpisode aep = s.AnimeEpisodeFromAniDB_Episode(e);
		                    bool newWatchedStatus = false;
                            if (isManualLink)
		                        newWatchedStatus = JMMService.AnidbProcessor.AddFileToMyList(user.Id, s.AniDB_Anime.Id, e.Number, ref watchedDate);
		                    else
		                        newWatchedStatus = JMMService.AnidbProcessor.AddFileToMyList(user.Id, new Hash {FileSize = vl.FileSize, ED2KHash = vl.Hash, Info = vl.FileInfo.Path ?? string.Empty}, ref watchedDate);
		                    vl.ToggleWatchedStatus(newWatchedStatus, false, watchedDate, user, false, true);
		                    logger.Info("Adding file to list: {0} - {1}", vl.ToString(), watchedDate);
		                    if (ServerSettings.Import_UseExistingFileWatchedStatus && !newWatchedStatus)
		                    {
		                        UserStats n = aep.UsersStats.FirstOrDefault(a => a.JMMUserId == user.Id);
		                        if ((n != null) && (n.WatchedCount > 0))
		                        {
		                            logger.Info("Setting file as watched, because episode was already watched: {0} - user: {1}",
		                                vl.ToString(), user.UserName);
		                            vl.ToggleWatchedStatus(true, true, n.WatchedDate, user, false, true);
		                        }
		                    }
                            Store.VideoLocalRepo.Save(vl);
		                    if (ServerSettings.Trakt_IsEnabled && !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
		                    {
		                        CommandRequest_TraktCollectionEpisode cmdSyncTrakt =
		                            new CommandRequest_TraktCollectionEpisode(aep.Id, TraktSyncAction.Add);
		                        cmdSyncTrakt.Save();
		                    }
		                }
		                JMMModels.JMMUser maluser = user.GetUserWithAuth(AuthorizationProvider.MAL);
		                if (maluser != null)
		                {
		                    UserNameAuthorization auth = maluser.GetMALAuthorization();
                            if (!string.IsNullOrEmpty(auth.UserName) && !string.IsNullOrEmpty(auth.Password))
                            {
                                CommandRequest_MALUpdatedWatchedStatus cmdMAL = new CommandRequest_MALUpdatedWatchedStatus(maluser.Id, int.Parse(s.AniDB_Anime.Id));
                                cmdMAL.Save();
                            }

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
