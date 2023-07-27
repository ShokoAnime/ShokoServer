using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Utilities;

namespace Shoko.Server.Providers.AniDB.Titles;

[XmlRoot("animetitles")]
public class ResponseAniDBTitles
{
    [XmlElement("anime")] public List<Anime> Animes { get; set; }

    public class Anime
    {
        [XmlIgnore]
        public string MainTitle =>
            (Titles?.FirstOrDefault(t => t.Language == TitleLanguage.Romaji && t.TitleType == TitleType.Main) ??
             Titles?.FirstOrDefault())?.Title ?? "";

        [XmlIgnore]
        public string PreferredTitle
        {
            get
            {
                // Check each preferred language in order.
                foreach (var language in Languages.PreferredNamingLanguages.Select(a => a.Language))
                {
                    // First check the main title.
                    var title = Titles.FirstOrDefault(title => title.TitleType == TitleType.Main && title.Language == language);
                    if (title != null) return title.Title;

                    // Then check for an official title.
                    title = Titles.FirstOrDefault(title => title.TitleType == TitleType.Official && title.Language == language);
                    if (title != null) return title.Title;

                    // Then check for _any_ title at all, if there is no main or official title in the langugage.
                    if (Utils.SettingsProvider.GetSettings().LanguageUseSynonyms)
                    {
                        title = Titles.FirstOrDefault(title => title.Language == language);
                        if (title != null) return title.Title;
                    }
                }

                // Otherwise just use the cached main title.
                return MainTitle;
            }
        }

        [XmlAttribute(DataType = "int", AttributeName = "aid")]
        public int AnimeID { get; set; }

        [XmlElement("title")] public List<AnimeTitle> Titles { get; set; }

        public class AnimeTitle
        {
            [XmlText] public string Title { get; set; }

            [XmlAttribute("type")] public TitleType TitleType { get; set; }

            [XmlIgnore] public TitleLanguage Language { get; set; }

            [XmlAttribute(DataType = "string", AttributeName = "xml:lang")]
            public string LanguageCode
            {
                get => Language.GetString();
                set => Language = value.GetTitleLanguage();
            }
        }
    }
}
