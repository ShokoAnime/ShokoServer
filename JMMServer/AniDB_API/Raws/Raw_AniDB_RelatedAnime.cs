using System;
using System.Xml;

namespace AniDBAPI
{
    [Serializable]
    public class Raw_AniDB_RelatedAnime : XMLBase
    {
        public Raw_AniDB_RelatedAnime()
        {
            InitFields();
        }

        public int AnimeID { get; set; }
        public int RelatedAnimeID { get; set; }
        public string RelationType { get; set; }

        private void InitFields()
        {
            AnimeID = 0;
            RelatedAnimeID = 0;
            RelationType = string.Empty;
        }

        public void ProcessFromHTTPResult(XmlNode node, int anid)
        {
            InitFields();

            AnimeID = anid;

            var id = 0;
            int.TryParse(AniDBHTTPHelper.TryGetAttribute(node, "id"), out id);
            RelatedAnimeID = id;

            RelationType = AniDBHTTPHelper.TryGetAttribute(node, "type");
        }
    }
}