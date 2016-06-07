using System;
using System.Xml;

namespace AniDBAPI
{
    [Serializable]
    public class Raw_AniDB_Tag : XMLBase
    {
        public void ProcessFromHTTPResult(XmlNode node, int anid)
        {
            AnimeID = anid;
            TagID = 0;
            Spoiler = 0;
            LocalSpoiler = 0;
            GlobalSpoiler = 0;
            TagName = "";
            TagDescription = "";
            TagCount = 0;
            Approval = 0;
            Weight = 0;

            TagID = int.Parse(AniDBHTTPHelper.TryGetAttribute(node, "id"));

            var tapp = 0;
            int.TryParse(AniDBHTTPHelper.TryGetAttribute(node, "weight"), out tapp);
            Weight = tapp;

            TagName = AniDBHTTPHelper.TryGetProperty(node, "name");
            TagDescription = AniDBHTTPHelper.TryGetProperty(node, "description");

            var lsp = false;
            bool.TryParse(AniDBHTTPHelper.TryGetAttribute(node, "localspoiler"), out lsp);
            Spoiler = lsp ? 1 : 0;

            var gsp = false;
            bool.TryParse(AniDBHTTPHelper.TryGetAttribute(node, "globalspoiler"), out gsp);
            Spoiler = gsp ? 1 : 0;
        }

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
    }
}