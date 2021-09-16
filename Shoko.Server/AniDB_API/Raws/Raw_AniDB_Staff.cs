using System;
using System.Text;
using System.Xml;

namespace AniDBAPI
{
    [Serializable]
    public class Raw_AniDB_Staff : XMLBase
    {
        public int AnimeID { get; set; }
        public int CreatorID { get; set; }
        public string CreatorName { get; set; }
        public string CreatorType { get; set; }

        public Raw_AniDB_Staff() {
            AnimeID = 0;
            CreatorID = 0;
            CreatorName = string.Empty;
            CreatorType = string.Empty;
        }

        public void ProcessFromHTTPResult(XmlNode node, int anid)
        {
            AnimeID = anid;
            CreatorID = int.Parse(AniDBHTTPHelper.TryGetAttribute(node, "id"));
            CreatorType = AniDBHTTPHelper.TryGetAttribute(node, "type");
            CreatorName = node.InnerText.Replace('`', '\'');
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("creatorID: " + CreatorID);
            sb.Append(" | creatorName: " + CreatorName);
            sb.Append(" | creatorType: " + CreatorType);

            return sb.ToString();
        }
    }
}
