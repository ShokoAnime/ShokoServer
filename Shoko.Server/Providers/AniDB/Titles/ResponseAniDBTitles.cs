using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;

namespace Shoko.Server.Providers.AniDB.Titles
{
    [XmlRoot("animetitles")]
    public class ResponseAniDBTitles
    {
        [XmlElement("anime")]
        public List<Anime> Animes { get; set; }

        public class Anime
        {
            [XmlIgnore]
            public virtual string MainTitle
            {
                get
                {
                    return (Titles?.FirstOrDefault(t => t.Language == TitleLanguage.Romaji && t.TitleType == TitleType.Main) ?? Titles?.FirstOrDefault())?.Title ?? "";
                }
            }

            [XmlAttribute(DataType = "int", AttributeName = "aid")]
            public int AnimeID { get; set; }

            [XmlElement("title")]
            public List<AnimeTitle> Titles { get; set; }

            public class AnimeTitle
            {
                [XmlText]
                public string Title { get; set; }

                [XmlAttribute("type")]
                public TitleType TitleType { get; set; }

                [XmlIgnore]
                public TitleLanguage Language { get; set; }

                [XmlAttribute(DataType = "string", AttributeName = "xml:lang")]
                public string LanguageCode
                {
                    get
                    {
                        return Language.GetString();
                    }
                    set
                    {
                        Language = value.GetTitleLanguage();
                    }
                }
            }
        }
    }
}