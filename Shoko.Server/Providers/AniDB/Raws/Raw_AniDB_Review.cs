using System.Text;

namespace Shoko.Server.Providers.AniDB.Raws
{
    public class Raw_AniDB_Review : XMLBase
    {
        public int ReviewID { get; set; }
        public int AuthorID { get; set; }
        public int RatingAnimation { get; set; }
        public int RatingSound { get; set; }
        public int RatingStory { get; set; }
        public int RatingCharacter { get; set; }
        public int RatingValue { get; set; }
        public int RatingEnjoyment { get; set; }
        public string ReviewText { get; set; }

        public Raw_AniDB_Review()
        {
            ReviewID = 0;
            AuthorID = 0;
            RatingAnimation = 0;
            RatingSound = 0;
            RatingStory = 0;
            RatingCharacter = 0;
            RatingValue = 0;
            RatingEnjoyment = 0;
            ReviewText = string.Empty;
        }

        public Raw_AniDB_Review(string sRecMessage)
        {
            // remove the header info
            string[] sDetails = sRecMessage.Substring(11).Split('|');

            // 234 REVIEW0|4||1198|18198|700|700|600|900|500|500|*EDIT* just an update, I stopped watching Naruto


            // 234 REVIEW
            // 0. 0 ** current part
            // 1. 4 ** max parts
            // 2.    ** blank
            // 3. 1198  ** review id
            // 4. 18198  ** author id
            // 5. 700  ** rating Animation
            // 6. 700  ** rating Sound
            // 7. 600  ** rating Story
            // 8. 900  ** rating Character
            // 9. 500  ** rating Value
            // 10. 500  ** rating Enjoyment
            // 11. *EDIT* just an update, I stopped watching Naruto  ** review text

            ReviewID = int.Parse(sDetails[3].Trim());
            AuthorID = int.Parse(sDetails[4].Trim());
            RatingAnimation = int.Parse(sDetails[5].Trim());
            RatingSound = int.Parse(sDetails[6].Trim());
            RatingStory = int.Parse(sDetails[7].Trim());
            RatingCharacter = int.Parse(sDetails[8].Trim());
            RatingValue = int.Parse(sDetails[9].Trim());
            RatingEnjoyment = int.Parse(sDetails[10].Trim());
            ReviewText = AniDBAPILib.ProcessAniDBString(sDetails[11].Trim());
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("reviewID: " + ReviewID.ToString());
            sb.Append(" | reviewText: " + ReviewText);

            return sb.ToString();
        }
    }
}