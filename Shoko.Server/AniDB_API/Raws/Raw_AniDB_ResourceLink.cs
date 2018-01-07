using System;
using System.Xml;
using Shoko.Models.Enums;

namespace AniDBAPI
{
    [Serializable]
    public class Raw_AniDB_ResourceLink : XMLBase
    {
        #region Properties

        public int AnimeID { get; set; }
        public AniDB_ResourceLinkType Type { get; set; }
        public string RawID { get; set; }

        public int ID
        {
            get
            {
                if (string.IsNullOrWhiteSpace(RawID)) return 0;
                bool result = int.TryParse(RawID, out int id);
                if (!result) return 0;
                return id;
            }
        }

        #endregion

        public Raw_AniDB_ResourceLink()
        {
        }

        public void ProcessFromHTTPResult(XmlNode node, int anid)
        {
            this.AnimeID = anid;

            bool result = int.TryParse(AniDBHTTPHelper.TryGetAttribute(node, "type"), out int typeInt);
            if (!result) return;
            Type = (AniDB_ResourceLinkType) typeInt;
        }
    }
}