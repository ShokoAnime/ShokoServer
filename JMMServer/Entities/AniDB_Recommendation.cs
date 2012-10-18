using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AniDBAPI;

namespace JMMServer.Entities
{
	public class AniDB_Recommendation
	{
		public int AniDB_RecommendationID { get; private set; }
		public int AnimeID { get; set; }
		public int UserID { get; set; }
		public int RecommendationType { get; set; }
		public string RecommendationText { get; set; }

		public void Populate(Raw_AniDB_Recommendation rawRec)
		{
			this.AnimeID = rawRec.AnimeID;
			this.UserID = rawRec.UserID;
			this.RecommendationText = rawRec.RecommendationText;

			RecommendationType = (int)AniDBRecommendationType.Recommended;

			if (rawRec.RecommendationTypeText.Equals("recommended", StringComparison.InvariantCultureIgnoreCase)) 
				RecommendationType = (int)AniDBRecommendationType.Recommended;

			if (rawRec.RecommendationTypeText.Equals("for fans", StringComparison.InvariantCultureIgnoreCase))
				RecommendationType = (int)AniDBRecommendationType.ForFans;

			if (rawRec.RecommendationTypeText.Equals("must see", StringComparison.InvariantCultureIgnoreCase))
				RecommendationType = (int)AniDBRecommendationType.MustSee;

		}

		public override string ToString()
		{
			return string.Format("Recommendation: {0} - {1}", UserID, RecommendationText);
		}
	}
}
