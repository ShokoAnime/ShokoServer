using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Repositories;
using JMMServer.Entities;
using System.Xml;
using AniDBAPI;
using JMMDatabase;
using JMMModels.ClientExtensions;
using JMMServerModels.DB.Childs;
using Raven.Client;

namespace JMMServer.Commands
{
	[Serializable]
	public class CommandRequest_GetReleaseGroupStatus : BaseCommandRequest, ICommandRequest
	{
		public int AnimeID { get; set; }
		public bool ForceRefresh { get; set; }

		public CommandRequestPriority DefaultPriority 
		{
			get { return CommandRequestPriority.Priority5; }
		}

		public string PrettyDescription
		{
			get
			{
				return $"Getting group status info from UDP API for Anime: {AnimeID}";
			}
		}

		public CommandRequest_GetReleaseGroupStatus()
		{
		}

		public CommandRequest_GetReleaseGroupStatus(int aid, bool forced)
		{
			this.AnimeID = aid;
			this.ForceRefresh = forced;
			this.CommandType = CommandRequestType.AniDB_GetReleaseGroupStatus;
            this.JMMUserId = Store.JmmUserRepo.GetMasterUser().Id;
            this.Priority = DefaultPriority;
            this.Id= $"CommandRequest_GetReleaseGroupStatus_{AnimeID}";
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_GetReleaseGroupStatus: {0}", AnimeID);

			try
			{
			    JMMModels.AnimeSerie serie = Store.AnimeSerieRepo.AnimeSerieFromAniDBAnime(AnimeID.ToString());
			    if (serie == null) return;

				// don't get group status if the anime has already ended more than 50 days ago
				bool skip = false;
				if (!ForceRefresh)
				{
				    DateTime? enddate = serie.AniDB_Anime.EndDate.ToDateTime();
					if (enddate.HasValue)
					{
						if (enddate.Value < DateTime.Now)
						{
							TimeSpan ts = DateTime.Now - enddate.Value;
							if (ts.TotalDays > 50)
							{
                                if (serie.AniDB_Anime.ReleaseGroups!=null && serie.AniDB_Anime.ReleaseGroups.Count>0)
								{
									skip = true;
								}
							}
						}
					}
				}

				if (skip)
				{
					logger.Info("Skipping group status command because anime has already ended: {0}", serie.AniDB_Anime.ToString());
					return;
				}

				GroupStatusCollection grpCol = JMMService.AnidbProcessor.GetReleaseGroupStatusUDP(JMMUserId, AnimeID);

				if (ServerSettings.AniDB_DownloadReleaseGroups)
				{
				    using (IDocumentSession session = Store.GetSession())
				    {
                        foreach (Raw_AniDB_GroupStatus grpStatus in grpCol.Groups)
                        {
                            CommandRequest_GetReleaseGroup cmdRelgrp = new CommandRequest_GetReleaseGroup(grpStatus.GroupID, false);
                            cmdRelgrp.Save(session);
                        }
                    }
				}
			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_GetReleaseGroupStatus: {0} - {1}", AnimeID, ex.ToString());
				return;
			}
		}

	}
}
