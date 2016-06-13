using System.Xml;

namespace AniDBAPI
{
    public class Raw_AniDB_Recommendation
    {
        public int AnimeID { get; set; }
        public int UserID { get; set; }
        public string RecommendationTypeText { get; set; }
        //public int RecommendationType { get; set; }
        public string RecommendationText { get; set; }

        public Raw_AniDB_Recommendation()
        {
            InitFields();
        }

        private void InitFields()
        {
            AnimeID = 0;
            UserID = 0;
            //RecommendationType = 0;

            RecommendationTypeText = string.Empty;
            RecommendationText = string.Empty;
        }

        public void ProcessFromHTTPResult(XmlNode node, int anid)
        {
            InitFields();

            this.AnimeID = anid;

            this.RecommendationTypeText = AniDBHTTPHelper.TryGetAttribute(node, "type");
            this.RecommendationText = node.InnerText.Trim();

            int uid = 0;
            int.TryParse(AniDBHTTPHelper.TryGetAttribute(node, "uid"), out uid);
            this.UserID = uid;
        }
    }
}