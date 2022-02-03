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

        public void ProcessFromHTTPResult(XmlNode node, int anid)
        {
            AnimeID = anid;
            TagID = 0;
            Spoiler = 0;
            LocalSpoiler = 0;
            GlobalSpoiler = 0;
            TagName = string.Empty;
            TagDescription = string.Empty;
            TagCount = 0;
            Approval = 0;
            Weight = 0;

            TagID = int.Parse(AniDBHTTPHelper.TryGetAttribute(node, "id"));

            int.TryParse(AniDBHTTPHelper.TryGetAttribute(node, "weight"), out int tapp);
            Weight = tapp;

            TagName = AniDBHTTPHelper.TryGetProperty(node, "name")?.Replace('`', '\'');
            TagDescription = AniDBHTTPHelper.TryGetProperty(node, "description")?.Replace('`', '\'');

            bool.TryParse(AniDBHTTPHelper.TryGetAttribute(node, "localspoiler"), out bool lsp);
            Spoiler |= lsp ? 1 : 0;

            bool.TryParse(AniDBHTTPHelper.TryGetAttribute(node, "globalspoiler"), out bool gsp);
            Spoiler |= gsp ? 1 : 0;
        }
    }
}