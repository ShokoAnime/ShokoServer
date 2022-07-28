using System.Collections.Generic;
using System.Xml.Serialization;

namespace Shoko.Server.Providers.AniDB.Titles
{
    [XmlRoot("animetitles")]
    public class ResponseAniDBTitles
    {
        [XmlElement("anime")]
        public List<Anime> Animes { get; set; }

        public class Anime
        {
            [XmlAttribute(DataType = "int", AttributeName = "aid")]
            public int AnimeID { get; set; }

            [XmlElement("title")]
            public List<AnimeTitle> Titles { get; set; }

            public class AnimeTitle
            {
                [XmlText]
                public string Title { get; set; }

                [XmlAttribute("type")]
                public string TitleType { get; set; }

                [XmlAttribute("xml:lang")]
                public string TitleLanguage { get; set; }
            }
        }
    }
}