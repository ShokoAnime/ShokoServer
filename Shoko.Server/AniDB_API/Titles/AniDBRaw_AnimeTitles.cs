using System.Collections.Generic;
using System.Linq;
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
        [XmlIgnore]
        public virtual string MainTitle
        {
            get
            {
                return (Titles.FirstOrDefault(t => t.TitleLanguage == "x-jat" && t.TitleType == "main") ?? Titles.FirstOrDefault()).Title;
            }
        }

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