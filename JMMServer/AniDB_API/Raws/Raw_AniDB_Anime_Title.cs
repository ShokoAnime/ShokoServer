using System;
using System.Xml;

namespace AniDBAPI
{
    [Serializable]
    public class Raw_AniDB_Anime_Title : XMLBase
    {
        public void ProcessFromHTTPResult(XmlNode node, int anid)
        {
            AnimeID = anid;
            TitleType = "";
            Language = "";
            Title = "";

            TitleType = AniDBHTTPHelper.TryGetAttribute(node, "type");
            Language = AniDBHTTPHelper.TryGetAttribute(node, "xml:lang");
            Title = node.InnerText.Trim();

            // Title Types
            // -------------
            // main
            // official
            // syn / SYNONYM / SYNONYMs
            // short

            // Common Languages
            // en = english
            // x-jat = romaji
            // ja = kanji
        }

        #region Properties

        public int AnimeID { get; set; }
        public string TitleType { get; set; }
        public string Language { get; set; }
        public string Title { get; set; }

        #endregion
    }
}