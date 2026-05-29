using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Models.Shoko;

#nullable enable
namespace Shoko.Server.Models.AniDB.Embedded;

public class AniDB_Season(IAnidbAnime anime, EpisodeType episodeType, int seasonNumber) : IAnidbSeason
{
    public static string GetID(int animeID, EpisodeType episodeType, int seasonNumber) => $"{animeID}:{episodeType}:{seasonNumber}";

    public string ID => GetID(anime.ID, episodeType, seasonNumber);

    private readonly string? _imagePath = ((AniDB_Anime)anime).Picname;

    int ISeason.SeriesID => anime.ID;

    int ISeason.SeasonNumber => seasonNumber;

    ISeries ISeason.Series => anime;

    IReadOnlyList<IEpisode> ISeason.Episodes => anime.Episodes
        .Where(x => x.Type == episodeType && x.SeasonNumber == seasonNumber)
        .ToList();

    string IWithTitles.Title
        => seasonNumber is 0
        ? "Specials"
        : anime.Title;

    ITitle IWithTitles.DefaultTitle
        => seasonNumber is 0
            ? new TitleStub
            {
                Language = TitleLanguage.English,
                LanguageCode = "en",
                Value = "Specials",
                Source = DataSource.Shoko,
                Type = TitleType.Official,
            }
            : anime.DefaultTitle;

    ITitle? IWithTitles.PreferredTitle
        => seasonNumber is 0
            ? new TitleStub
            {
                Language = TitleLanguage.English,
                LanguageCode = "en",
                Value = "Specials",
                Source = DataSource.Shoko,
                Type = TitleType.Official,
            }
            : anime.PreferredTitle;

    IReadOnlyList<ITitle> IWithTitles.Titles => seasonNumber is 0
        ? [
            new TitleStub
            {
                Language = TitleLanguage.English,
                LanguageCode = "en",
                Value = "Specials",
                Source = DataSource.Shoko,
                Type = TitleType.Official,
            },
        ]
        : anime.Titles;

    IText? IWithDescriptions.DefaultDescription
        => seasonNumber is 0
            ? new TextStub
            {
                Language = TitleLanguage.English,
                LanguageCode = "en",
                Value = "Specials",
                Source = DataSource.Shoko,
            }
            : anime.DefaultDescription;

    IText? IWithDescriptions.PreferredDescription
        => seasonNumber is 0
            ? new TextStub
            {
                Language = TitleLanguage.English,
                LanguageCode = "en",
                Value = "Specials",
                Source = DataSource.Shoko,
            }
            : anime.PreferredDescription;

    IReadOnlyList<IText> IWithDescriptions.Descriptions => seasonNumber is 0
        ? [
            new TextStub
            {
                Language = TitleLanguage.English,
                LanguageCode = "en",
                Value = "Specials",
                Source = DataSource.Shoko,
            },
        ]
        : anime.Descriptions;

    DateTime IWithUpdateDate.LastUpdatedAt => anime.LastUpdatedAt;

    IReadOnlyList<ICast> IWithCastAndCrew.Cast => anime.Cast;

    IReadOnlyList<ICrew> IWithCastAndCrew.Crew => anime.Crew;

    DataSource IMetadata.Source => DataSource.AniDB;

    IAnidbAnime IAnidbSeason.Series => anime;

    IReadOnlyList<IAnidbEpisode> IAnidbSeason.Episodes => anime.Episodes
        .Where(x => x.Type == episodeType && x.SeasonNumber == seasonNumber)
        .ToList();

    IReadOnlyList<(int Year, YearlySeason Season)> IWithYearlySeasons.YearlySeasons
        => seasonNumber is 0 ? [] : anime.YearlySeasons;

    #region IWithPrimaryImage Implementation

    public IImage? DefaultPrimaryImage => DefaultPrimaryImageCrossReference is { } xref && xref.GetImage() is { } image
        ? new ShokoImageStub(image, xref)
        : null;

    public IImageCrossReference? DefaultPrimaryImageCrossReference => !string.IsNullOrEmpty(_imagePath) && IImageManager.GetIDForImageSourceAndResourceID(DataSource.AniDB, _imagePath) is { } posterID
        ? (this as IWithImages).GetImageCrossReferences(imageType: ImageEntityType.Primary).FirstOrDefault(xref => xref.ImageID == posterID)
        : null;

    #endregion
}
