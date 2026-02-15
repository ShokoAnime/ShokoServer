using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Providers.AniDB.Titles;

[XmlRoot("animetitles")]
public class ResponseAniDBTitles
{
    [XmlElement("anime")] public List<Anime> AnimeList { get; set; } = [];

    public class Anime
    {
        [XmlIgnore]
        public string Title => (PreferredTitle ?? DefaultTitle).Value;

        [XmlIgnore]
        public ITitle DefaultTitle =>
            Titles.FirstOrDefault(t => t.TitleType == TitleType.Main) as ITitle ??
            Titles.FirstOrDefault() as ITitle ??
            new TitleStub()
            {
                Language = TitleLanguage.Unknown,
                LanguageCode = "unk",
                Value = $"<AniDB Anime {AnimeID}>",
                Source = DataSource.None,
            };

        [XmlIgnore]
        public ITitle? PreferredTitle
        {
            get
            {
                // Check each preferred language in order.
                foreach (var language in Languages.PreferredNamingLanguages.Select(a => a.Language))
                {
                    // First check the main title.
                    var title = Titles.FirstOrDefault(title => title.TitleType is TitleType.Main && title.Language == language);
                    if (title != null)
                        return title;

                    // Then check for an official title.
                    title = Titles.FirstOrDefault(t => t.TitleType is TitleType.Official && t.Language == language);
                    if (title != null)
                        return title;

                    // Then check for _any_ title at all, if there is no main or official title in the language.
                    if (Utils.SettingsProvider.GetSettings().Language.UseSynonyms)
                    {
                        title = Titles.FirstOrDefault(t => t.Language == language);
                        if (title != null)
                            return title;
                    }
                }

                // Otherwise just use the cached main title.
                return null;
            }
        }

        [XmlAttribute(DataType = "int", AttributeName = "aid")]
        public int AnimeID { get; set; }

        [XmlElement("title")] public List<AnimeTitle> Titles { get; set; } = [];

        public class AnimeTitle : ITitle
        {
            [XmlText] public string Title { get; set; } = string.Empty;

            [XmlAttribute("type")] public TitleType TitleType { get; set; }

            [XmlIgnore] public TitleLanguage Language { get; set; }

            [XmlAttribute(DataType = "string", AttributeName = "xml:lang")]
            public string LanguageCode
            {
                get => Language.GetString();
                set => Language = value.GetTitleLanguage();
            }

            public bool Equals(IText? other)
                => IText.Equals(this, other);

            public bool Equals(ITitle? other)
                => ITitle.Equals(this, other);

            TitleType ITitle.Type => TitleType;

            string? IText.CountryCode => null;

            string IText.Value => Title;

            DataSource IMetadata.Source => DataSource.AniDB;
        }
    }
}
