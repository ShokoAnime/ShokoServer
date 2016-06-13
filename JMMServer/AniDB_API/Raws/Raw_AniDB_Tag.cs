using System;
using System.Xml;

namespace AniDBAPI
{
    [Serializable]
    public class Raw_AniDB_Tag : XMLBase
    {
        #region Properties

        public int AnimeID { get; set; }
        public int TagID { get; set; }
        public int Spoiler { get; set; }
        public int LocalSpoiler { get; set; }
        public int GlobalSpoiler { get; set; }
        public string TagName { get; set; }
        public string TagDescription { get; set; }
        public int TagCount { get; set; }
        public int Approval { get; set; }
        public int Weight { get; set; }

        #endregion

        public Raw_AniDB_Tag()
        {
        }

        public void ProcessFromHTTPResult(XmlNode node, int anid)
        {
            this.AnimeID = anid;
            this.TagID = 0;
            this.Spoiler = 0;
            this.LocalSpoiler = 0;
            this.GlobalSpoiler = 0;
            this.TagName = "";
            this.TagDescription = "";
            this.TagCount = 0;
            this.Approval = 0;
            this.Weight = 0;

            this.TagID = int.Parse(AniDBHTTPHelper.TryGetAttribute(node, "id"));

            int tapp = 0;
            int.TryParse(AniDBHTTPHelper.TryGetAttribute(node, "weight"), out tapp);
            this.Weight = tapp;

            this.TagName = AniDBHTTPHelper.TryGetProperty(node, "name");
            this.TagDescription = AniDBHTTPHelper.TryGetProperty(node, "description");

            bool lsp = false;
            bool.TryParse(AniDBHTTPHelper.TryGetAttribute(node, "localspoiler"), out lsp);
            this.Spoiler = lsp ? 1 : 0;

            bool gsp = false;
            bool.TryParse(AniDBHTTPHelper.TryGetAttribute(node, "globalspoiler"), out gsp);
            this.Spoiler = gsp ? 1 : 0;
        }
    }
}