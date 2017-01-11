using System;
using AniDBAPI;
using Shoko.Models;
using Shoko.Models.Server;

namespace Shoko.Server.Entities
{
    public class SVR_AniDB_Recommendation : AniDB_Recommendation
    {
        public SVR_AniDB_Recommendation() //Empty Constructor for nhibernate
        {

        }
        public void Populate(Raw_AniDB_Recommendation rawRec)
        {
            this.AnimeID = rawRec.AnimeID;
            this.UserID = rawRec.UserID;
            this.RecommendationText = rawRec.RecommendationText;

            RecommendationType = (int) AniDBRecommendationType.Recommended;

            if (rawRec.RecommendationTypeText.Equals("recommended", StringComparison.InvariantCultureIgnoreCase))
                RecommendationType = (int) AniDBRecommendationType.Recommended;

            if (rawRec.RecommendationTypeText.Equals("for fans", StringComparison.InvariantCultureIgnoreCase))
                RecommendationType = (int) AniDBRecommendationType.ForFans;

            if (rawRec.RecommendationTypeText.Equals("must see", StringComparison.InvariantCultureIgnoreCase))
                RecommendationType = (int) AniDBRecommendationType.MustSee;
        }

        public override string ToString()
        {
            return string.Format("Recommendation: {0} - {1}", UserID, RecommendationText);
        }

    }
}