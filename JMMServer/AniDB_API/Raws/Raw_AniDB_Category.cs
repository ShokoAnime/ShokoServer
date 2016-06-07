using System;
using System.Xml;

namespace AniDBAPI
{
    [Serializable]
    public class Raw_AniDB_Category : XMLBase
    {
        public void ProcessFromHTTPResult(XmlNode node, int anid)
        {
            AnimeID = anid;
            CategoryID = 0;
            ParentID = 0;
            IsHentai = 0;
            CategoryName = "";
            CategoryDescription = "";
            Weighting = 0;

            CategoryID = int.Parse(AniDBHTTPHelper.TryGetAttribute(node, "id"));
            ParentID = int.Parse(AniDBHTTPHelper.TryGetAttribute(node, "parentid"));

            var hentai = false;
            bool.TryParse(AniDBHTTPHelper.TryGetAttribute(node, "hentai"), out hentai);
            IsHentai = hentai ? 1 : 0;


            CategoryName = AniDBHTTPHelper.TryGetProperty(node, "name");
            CategoryDescription = AniDBHTTPHelper.TryGetProperty(node, "description");

            var weight = 0;
            int.TryParse(AniDBHTTPHelper.TryGetAttribute(node, "weight"), out weight);
            Weighting = weight;
        }

        #region Properties

        public int AnimeID { get; set; }
        public int CategoryID { get; set; }
        public int ParentID { get; set; }
        public int IsHentai { get; set; }
        public string CategoryName { get; set; }
        public string CategoryDescription { get; set; }
        public int Weighting { get; set; }

        #endregion
    }
}