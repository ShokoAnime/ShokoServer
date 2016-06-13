using System;
using System.Xml;

namespace AniDBAPI
{
    [Serializable]
    public class Raw_AniDB_Category : XMLBase
    {
        #region Properties

        public int AnimeID { get; set; }
        public int CategoryID { get; set; }
        public int ParentID { get; set; }
        public int IsHentai { get; set; }
        public string CategoryName { get; set; }
        public string CategoryDescription { get; set; }
        public int Weighting { get; set; }

        #endregion

        public Raw_AniDB_Category()
        {
        }

        public void ProcessFromHTTPResult(XmlNode node, int anid)
        {
            this.AnimeID = anid;
            this.CategoryID = 0;
            this.ParentID = 0;
            this.IsHentai = 0;
            this.CategoryName = "";
            this.CategoryDescription = "";
            this.Weighting = 0;

            this.CategoryID = int.Parse(AniDBHTTPHelper.TryGetAttribute(node, "id"));
            this.ParentID = int.Parse(AniDBHTTPHelper.TryGetAttribute(node, "parentid"));

            bool hentai = false;
            bool.TryParse(AniDBHTTPHelper.TryGetAttribute(node, "hentai"), out hentai);
            this.IsHentai = hentai ? 1 : 0;


            this.CategoryName = AniDBHTTPHelper.TryGetProperty(node, "name");
            this.CategoryDescription = AniDBHTTPHelper.TryGetProperty(node, "description");

            int weight = 0;
            int.TryParse(AniDBHTTPHelper.TryGetAttribute(node, "weight"), out weight);
            this.Weighting = weight;
        }
    }
}