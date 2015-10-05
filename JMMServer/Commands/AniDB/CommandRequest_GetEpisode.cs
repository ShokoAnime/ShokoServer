using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using System.IO;
using JMMServer.Repositories;
using JMMContracts;
using JMMFileHelper;
using AniDBAPI;
using System.Xml;
using JMMDatabase;
using JMMServer.WebCache;
using JMMServerModels.DB.Childs;

namespace JMMServer.Commands.AniDB 
{
    [Serializable]
    public class CommandRequest_GetEpisode : BaseCommandRequest, ICommandRequest
    {
        public int EpisodeID { get; set; }

		public CommandRequestPriority DefaultPriority
		{
			get { return CommandRequestPriority.Priority4; }
		}

		public string PrettyDescription
		{
			get
			{
                return string.Format("Getting episode info from UDP API: {0}", EpisodeID);
			}
		}

		public CommandRequest_GetEpisode()
		{
		}

        public CommandRequest_GetEpisode(int epID)
		{
            this.EpisodeID = epID;
            this.CommandType = CommandRequestType.AniDB_GetEpisodeUDP;
			this.Priority = DefaultPriority;
            this.JMMUserId = Store.JmmUserRepo.GetMasterUser().Id;
            this.Id= $"CommandRequest_GetEpisode_{EpisodeID}";
		}
        //TODO
        //TODO
        //TODO
        //TODO

		public override void ProcessCommand()
		{
            logger.Info("Get AniDB episode info: {0}", EpisodeID);

			
			try
			{
                // we don't use this command to update episode info
                // we actually use it to update the cross ref info instead
                // and we only use it for the "Other Episodes" section of the FILE command
                // because that field doesn't tell you what anime it belongs to

			    JMMModels.AnimeSerie ser = Store.AnimeSerieRepo.AnimeSerieFromAniDBEpisode(EpisodeID.ToString());
			    if (ser == null)
			        return;

                /*
                CrossRef_File_EpisodeRepository repCrossRefs = new CrossRef_File_EpisodeRepository();
                List<CrossRef_File_Episode> xrefs = repCrossRefs.GetByEpisodeID(EpisodeID);
                if (xrefs.Count == 0) return;
                */
                Raw_AniDB_Episode epInfo = JMMService.AnidbProcessor.GetEpisodeInfo(EpisodeID);
                if (epInfo != null)
				{
				    if (ser.AniDB_Anime.Id != epInfo.AnimeID.ToString())
				    {
				        



				    }

                    AnimeSeriesRepository repSeries = new AnimeSeriesRepository();

                    foreach (CrossRef_File_Episode xref in xrefs)
                    {
                        int oldAnimeID = xref.AnimeID;
                        xref.AnimeID = epInfo.AnimeID;
                        repCrossRefs.Save(xref);


                        AnimeSeries ser = repSeries.GetByAnimeID(oldAnimeID);
                        if (ser != null)
                            ser.QueueUpdateStats();
                        StatsCache.Instance.UpdateUsingAnime(oldAnimeID);

                        ser = repSeries.GetByAnimeID(epInfo.AnimeID);
                        if (ser != null)
                            ser.QueueUpdateStats();
                        StatsCache.Instance.UpdateUsingAnime(epInfo.AnimeID);
                    }
				}
				
			}
			catch (Exception ex)
			{
                logger.Error("Error processing CommandRequest_GetEpisode: {0} - {1}", EpisodeID, ex.ToString());
				return;
			}
		}

    }
}
