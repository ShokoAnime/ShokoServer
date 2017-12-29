using System;
using System.Xml;

namespace AniDBAPI
{
    [Serializable]
    public class Raw_AniDB_SimilarAnime : XMLBase
    {
        public int AnimeID { get; set; }
        public int SimilarAnimeID { get; set; }
        public int Approval { get; set; }
        public int Total { get; set; }

        public Raw_AniDB_SimilarAnime()
        {
            InitFields();
        }

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

            this.AnimeID = anid;

            int.TryParse(AniDBHTTPHelper.TryGetAttribute(node, "id"), out int id);
            this.SimilarAnimeID = id;

            int.TryParse(AniDBHTTPHelper.TryGetAttribute(node, "approval"), out int appr);
            this.Approval = appr;

            int.TryParse(AniDBHTTPHelper.TryGetAttribute(node, "total"), out int tot);
            this.Total = tot;
        }
    }
}