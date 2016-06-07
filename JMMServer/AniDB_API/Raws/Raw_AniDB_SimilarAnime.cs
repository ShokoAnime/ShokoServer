using System;
using System.Xml;

namespace AniDBAPI
{
    [Serializable]
    public class Raw_AniDB_SimilarAnime : XMLBase
    {
        public Raw_AniDB_SimilarAnime()
        {
            InitFields();
        }

        public int AnimeID { get; set; }
        public int SimilarAnimeID { get; set; }
        public int Approval { get; set; }
        public int Total { get; set; }

        private void InitFields()
        {
            AnimeID = 0;
            SimilarAnimeID = 0;
            Approval = 0;
            Total = 0;
        }

        public void ProcessFromHTTPResult(XmlNode node, int anid)
        {
            InitFields();

            AnimeID = anid;

            var id = 0;
            int.TryParse(AniDBHTTPHelper.TryGetAttribute(node, "id"), out id);
            SimilarAnimeID = id;

            var appr = 0;
            int.TryParse(AniDBHTTPHelper.TryGetAttribute(node, "approval"), out appr);
            Approval = appr;

            var tot = 0;
            int.TryParse(AniDBHTTPHelper.TryGetAttribute(node, "total"), out tot);
            Total = tot;
        }
    }
}