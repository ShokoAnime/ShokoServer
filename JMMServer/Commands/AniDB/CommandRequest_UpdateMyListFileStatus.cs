using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using System.Xml;
using AniDBAPI;
using JMMDatabase;
using JMMDatabase.Extensions;
using JMMModels;
using JMMModels.Childs;
using JMMServer.Repositories;
using JMMServerModels.DB.Childs;
using AnimeEpisode = JMMServer.Entities.AnimeEpisode;

namespace JMMServer.Commands
{
	[Serializable]
	public class CommandRequest_UpdateMyListFileStatus : BaseCommandRequest, ICommandRequest
	{
		public string FullFileName { get; set; }
		public string Hash { get; set; }
		public bool Watched { get; set; }
		public bool UpdateSeriesStats { get; set; }
		public int WatchedDateAsSecs { get; set; }
        public long FileSize { get; set; }

		public CommandRequestPriority DefaultPriority
		{
			get { return CommandRequestPriority.Priority8; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Updating MyList info from UDP API for File: {0}", FullFileName);
			}
		}

		public CommandRequest_UpdateMyListFileStatus()
		{
		}

		public CommandRequest_UpdateMyListFileStatus(string userid, string hash, long filesize, bool watched, bool updateSeriesStats, int watchedDateSecs)
		{
			this.Hash = hash;
			this.Watched = watched;
		    this.FileSize = filesize;
			this.CommandType = CommandRequestType.AniDB_UpdateWatchedUDP;
			this.Priority = DefaultPriority;
			this.UpdateSeriesStats = updateSeriesStats;
			this.WatchedDateAsSecs = watchedDateSecs;
		    this.JMMUserId = userid;
		    this.Id = $"CommandRequest_UpdateMyListFileStatus_{Hash}_{Guid.NewGuid()}";
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_UpdateMyListFileStatus: {0}", Hash);

			
			try
			{
			    JMMModels.VideoLocal vid = Store.VideoLocalRepo.Find(Hash, FileSize);

				VideoLocalRepository repVids = new VideoLocalRepository();

				// NOTE - we might return more than one VideoLocal record here, if there are duplicates by hash
				if (vid != null)
				{
                    JMMModels.JMMUser user = Store.JmmUserRepo.Find(JMMUserId).GetUserWithAuth(AuthorizationProvider.AniDB);
                    if (user == null)
                        return;


                    bool isManualLink = vid.CrossRefSource != CrossRefSourceType.AniDB;
                    
					if (isManualLink)
					{
					    List<AnimeSerie> sers = Store.AnimeSerieRepo.AnimeSeriesFromVideoLocal(vid.Id);
					    foreach (AnimeSerie s in sers)
					    {
					        foreach (JMMModels.AniDB_Episode eos in s.AniDB_EpisodesFromVideoLocal(vid))
					        {
                                JMMService.AnidbProcessor.UpdateMyListFileStatus(user.Id, int.Parse(s.AniDB_Anime.Id), eos.Number, this.Watched);
                                logger.Info("Updating file list status (GENERIC): {0} - {1}", vid.ToString(), this.Watched);
                            }
                        }
					}
					else
					{
						if (WatchedDateAsSecs > 0)
						{
							DateTime? watchedDate = Utils.GetAniDBDateAsDate(WatchedDateAsSecs);
							JMMService.AnidbProcessor.UpdateMyListFileStatus(user.Id, new Hash { ED2KHash = vid.Hash, FileSize = vid.FileSize, Info=vid.FileInfo.Path}, this.Watched, watchedDate);
						}
						else
							JMMService.AnidbProcessor.UpdateMyListFileStatus(user.Id, new Hash { ED2KHash = vid.Hash, FileSize = vid.FileSize, Info = vid.FileInfo.Path}, this.Watched, null);
						logger.Info("Updating file list status: {0} - {1}", vid.ToString(), this.Watched);
					}
                    /*
					if (UpdateSeriesStats)
					{
						// update watched stats
						List<AnimeEpisode> eps = repEpisodes.GetByHash(vid.ED2KHash);
						if (eps.Count > 0)
						{
							// all the eps should belong to the same anime
							eps[0].GetAnimeSeries().QueueUpdateStats();
							//eps[0].AnimeSeries.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, false);
						}
					}*/
				}
			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_UpdateMyListFileStatus: {0} - {1}", Hash, ex.ToString());
			}
		}
	}
}
