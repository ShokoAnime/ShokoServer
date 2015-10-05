using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Repositories;
using JMMServer.Entities;
using System.Xml;
using AniDBAPI;
using JMMDatabase;
using JMMServerModels.DB.Childs;

namespace JMMServer.Commands
{
	[Serializable]
	public class CommandRequest_GetReviews : BaseCommandRequest, ICommandRequest
	{
		public int AnimeID { get; set; }
		public bool ForceRefresh { get; set; }

		public CommandRequestPriority DefaultPriority 
		{
			get { return CommandRequestPriority.Priority9; }
		}

		public string PrettyDescription
		{
			get
			{
				return $"Getting review info from UDP API for Anime: {AnimeID}";
			}
		}

		public CommandRequest_GetReviews()
		{
		}

		public CommandRequest_GetReviews(int animeid, bool forced)
		{
			this.AnimeID = animeid;
			this.ForceRefresh = forced;
            this.JMMUserId = Store.JmmUserRepo.GetMasterUser().Id;
            this.CommandType = CommandRequestType.AniDB_GetReviews;
			this.Priority = DefaultPriority;
            this.Id= $"CommandRequest_GetReviews_{AnimeID}";
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_GetReviews: {0}", AnimeID);

			try
			{


                JMMModels.AnimeSerie serie = Store.AnimeSerieRepo.AnimeSerieFromAniDBAnime(AnimeID.ToString());
                if (serie == null) return;
                // reviews count will be 0 when the anime is only downloaded via HTTP
			    if (ForceRefresh || serie.AniDB_Anime.Reviews==null || serie.AniDB_Anime.Reviews.Count== 0)
			    {
			        List<int> revids = JMMService.AnidbProcessor.GetReviewsFromAnimeInfoUDP(JMMUserId, AnimeID, true);
                    List<JMMModels.Childs.AniDB_Anime_Review> reviews=new List<JMMModels.Childs.AniDB_Anime_Review>();
			        foreach (int i in revids)
			        {
                        Raw_AniDB_Review rw=JMMService.AnidbProcessor.GetReviewUDP(JMMUserId, i);
			            if (rw != null)
			            {
                            JMMModels.Childs.AniDB_Anime_Review r =new JMMModels.Childs.AniDB_Anime_Review();
                            rw.Populate(r);
			                reviews.Add(r);
			            }
                    }
			        serie.AniDB_Anime.Reviews = reviews;
                    Store.AnimeSerieRepo.Save(serie,UpdateType.None);
                }
			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_GetReviews: {0} - {1}", AnimeID, ex.ToString());
				return;
			}
		}

	}
}
