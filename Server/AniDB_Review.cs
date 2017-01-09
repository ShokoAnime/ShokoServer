
namespace Shoko.Models.Server
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
    }
}