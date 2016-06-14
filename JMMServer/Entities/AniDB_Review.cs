using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AniDBAPI;

namespace JMMServer.Entities
{
	public class AniDB_Review
	{
		public int AniDB_ReviewID { get; private set; }
		public int ReviewID { get; set; }
		public int AuthorID { get; set; }
		public int RatingAnimation { get; set; }
		public int RatingSound { get; set; }
		public int RatingStory { get; set; }
		public int RatingCharacter { get; set; }
		public int RatingValue { get; set; }
		public int RatingEnjoyment { get; set; }
		public string ReviewText { get; set; }

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
