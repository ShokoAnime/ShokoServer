using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Repositories;
using JMMServer.Entities;
using System.Xml;
using JMMDatabase;
using JMMDatabase.Extensions;
using JMMServer.Commands.MAL;
using JMMServerModels.DB.Childs;

namespace JMMServer.Commands
{
	[Serializable]
	public class CommandRequest_DeleteFileFromMyList : BaseCommandRequest, ICommandRequest
	{
		public string HashAndSize { get; set; }
		public int FileID { get; set; }

		public CommandRequestPriority DefaultPriority
		{
			get { return CommandRequestPriority.Priority9; }
		}

		public string PrettyDescription
		{
			get
			{
                JMMModels.JMMUser u = Store.JmmUserRepo.Find(JMMUserId);
                return $"Deleting file from user {u.UserName} MyList: {HashAndSize}_{FileID}";
			}
		}

		public CommandRequest_DeleteFileFromMyList()
		{
		}

		public CommandRequest_DeleteFileFromMyList(string userid, string hashandsize)
		{
		    this.JMMUserId = userid;
		    this.HashAndSize = hashandsize;
			this.FileID = -1;
			this.CommandType = CommandRequestType.AniDB_DeleteFileUDP;
			this.Priority = DefaultPriority;
            this.Id= $"CommandRequest_DeleteFileFromMyList_Hash_{userid}_{HashAndSize}";
		}

		public CommandRequest_DeleteFileFromMyList(string userid, int fileId)
		{
		    this.JMMUserId = userid;
			this.FileID = fileId;
			this.CommandType = CommandRequestType.AniDB_DeleteFileUDP;
			this.Priority = DefaultPriority;
            this.Id = $"CommandRequest_DeleteFileFromMyList_File_{userid}_{FileID}";
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing {0}",Id);
		    JMMModels.JMMUser user = Store.JmmUserRepo.Find(JMMUserId).GetAniDBUser();
			try
			{
				if (ServerSettings.AniDB_MyList_DeleteType == AniDBAPI.AniDBFileDeleteType.Delete)
				{
				    if (FileID > 0)
				        JMMService.AnidbProcessor.DeleteFileFromMyList(user.Id, FileID);
				    else
				    {
				        string[] s = HashAndSize.Split('_');
                        JMMService.AnidbProcessor.DeleteFileFromMyList(user.Id, s[0],long.Parse(s[1]));
                    }

                    logger.Info("Deleting file from list: {0}_{1}", HashAndSize, FileID);
				}
                else if (ServerSettings.AniDB_MyList_DeleteType == AniDBAPI.AniDBFileDeleteType.MarkDeleted)
                {
                    if (FileID < 0)
                    {
                        string[] s = HashAndSize.Split('_');
                        JMMService.AnidbProcessor.MarkFileAsDeleted(user.Id, s[0], long.Parse(s[1]));
                        logger.Info("Marking file as deleted for user {1} from list: {0}", HashAndSize,user.UserName);
                    }
                }
                else
                {
                    if (FileID < 0)
                    {
                        string[] s = HashAndSize.Split('_');
                        JMMService.AnidbProcessor.MarkFileAsExternalStorage(user.Id, s[0], long.Parse(s[1]));
                        logger.Info("Moving File to external storage for user {1} : {0}", HashAndSize, user.UserName);
                    }
                }

                if (ServerSettings.AniDB_MyList_DeleteType == AniDBAPI.AniDBFileDeleteType.Delete ||
                    ServerSettings.AniDB_MyList_DeleteType == AniDBAPI.AniDBFileDeleteType.MarkDeleted)
                {
                    /*VideoLocalRepository repVids = new VideoLocalRepository();
                    VideoLocal vid = repVids.GetByHash(this.Hash);

                    // lets also try adding to the users trakt collecion
                    if (ServerSettings.Trakt_IsEnabled && !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
                    {
                        AnimeEpisodeRepository repEpisodes = new AnimeEpisodeRepository();
                        List<AnimeEpisode> animeEpisodes = vid.GetAnimeEpisodes();

                        foreach (AnimeEpisode aep in animeEpisodes)
                        {
                            CommandRequest_TraktCollectionEpisode cmdSyncTrakt = new CommandRequest_TraktCollectionEpisode(aep.AnimeEpisodeID, TraktSyncAction.Remove);
                            cmdSyncTrakt.Save();
                        }

                    }*/

                    // By the time we get to this point, the VideoLocal records would have been deleted
                    // So we can't get the episode records to do this on an ep by ep basis
                    // lets also try adding to the users trakt collecion by sync'ing the series

                }
                
			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_AddFileToMyList: {0}_{1} - {2}", HashAndSize, FileID, ex.ToString());
			}
		}
	}
}
