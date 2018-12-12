using System.Collections.Generic;
using System.Xml.Serialization;

namespace Shoko.Server.AniDB_API.Titles
{
    [XmlRoot("animetitles")]
    public class AniDBRaw_AnimeTitles
    {
        [XmlElement("anime")]
        public List<AniDBRaw_AnimeTitle_Anime> Animes { get; set; }
    }
    
    public class AniDBRaw_AnimeTitle_Anime
    {
        [XmlAttribute(DataType = "int", AttributeName = "aid")]
        public int AnimeID { get; set; }

        [XmlElement("title")]
        public List<AniDBRaw_AnimeTitle> Titles { get; set; }
        
    }

    public class AniDBRaw_AnimeTitle
    {
        [XmlText]
        public string Title { get; set; }

        [XmlAttribute("type")]
        public string TitleType { get; set; }

        [XmlAttribute("xml:lang")]
        public string TitleLanguage { get; set; }
    }
}