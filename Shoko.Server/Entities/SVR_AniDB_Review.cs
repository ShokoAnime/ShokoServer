using AniDBAPI;
using Shoko.Models.Server;

namespace Shoko.Server.Entities
{
    public class SVR_AniDB_Review : AniDB_Review
    {
        public void Populate(Raw_AniDB_Review rawReview)
        {
            this.ReviewID = rawReview.ReviewID;
            this.AuthorID = rawReview.AuthorID;
            this.RatingAnimation = rawReview.RatingAnimation;
            this.RatingSound = rawReview.RatingSound;
            this.RatingStory = rawReview.RatingStory;
            this.RatingCharacter = rawReview.RatingCharacter;
            this.RatingValue = rawReview.RatingValue;
            this.RatingEnjoyment = rawReview.RatingEnjoyment;
            this.ReviewText = rawReview.ReviewText;
        }

        public override string ToString()
        {
            return string.Format("Review: {0} - {1}", ReviewID, RatingEnjoyment);
        }
    }
}