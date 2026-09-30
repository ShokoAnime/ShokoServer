using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Services;

namespace Shoko.Server.Models.AniDB.Embedded;

public class AniDB_Season(IAnidbAnime anime, EpisodeType episodeType, int seasonNumber) : IAnidbSeason
{
    public static string GetID(int animeID, EpisodeType episodeType, int seasonNumber) => $"{animeID}:{episodeType}:{seasonNumber}";

    MetadataGuid IMetadata.ID => new(MetadataSource.AniDB, MetadataEntityType.Season, GetID(anime.AnidbID, episodeType, seasonNumber));

    private readonly string? _imagePath = ((AniDB_Anime)anime).Picname;

    int IAnidbSeason.AnidbAnimeID => anime.AnidbID;

    int ISeason.SeasonNumber => seasonNumber;

    ISeries ISeason.Series => anime;

    IReadOnlyList<IEpisode> ISeason.Episodes => anime.Episodes
        .Where(x => x.Type == episodeType && x.SeasonNumber == seasonNumber)
        .ToList();

    IReadOnlyList<IMetadataSeasonCrossReference> ISeason.MetadataSeasonCrossReferences
        => MetadataService.GetSeasonCrossReferences(this, ((ISeason)this).MetadataEpisodeCrossReferences);

    IReadOnlyList<IMetadataEpisodeCrossReference> ISeason.MetadataEpisodeCrossReferences
        => [.. ((ISeason)this).Episodes.SelectMany(episode => episode.MetadataEpisodeCrossReferences)];

    IReadOnlyList<IMetadataMovieCrossReference> ISeason.MetadataMovieCrossReferences
        => [.. ((ISeason)this).Episodes.SelectMany(episode => episode.MetadataMovieCrossReferences)];

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
                Source = MetadataSource.Shoko,
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
                Source = MetadataSource.Shoko,
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
                Source = MetadataSource.Shoko,
                Type = TitleType.Official,
            },
        ]
        : anime.Titles;

    IText? IWithOverviews.DefaultOverview
        => seasonNumber is 0
            ? new TextStub
            {
                Language = TitleLanguage.English,
                LanguageCode = "en",
                Value = "Specials",
                Source = MetadataSource.Shoko,
            }
            : anime.DefaultOverview;

    IText? IWithOverviews.PreferredOverview
        => seasonNumber is 0
            ? new TextStub
            {
                Language = TitleLanguage.English,
                LanguageCode = "en",
                Value = "Specials",
                Source = MetadataSource.Shoko,
            }
            : anime.PreferredOverview;

    IReadOnlyList<IText> IWithOverviews.Overviews => seasonNumber is 0
        ? [
            new TextStub
            {
                Language = TitleLanguage.English,
                LanguageCode = "en",
                Value = "Specials",
                Source = MetadataSource.Shoko,
            },
        ]
        : anime.Overviews;

    DateTime IWithUpdateDate.LastUpdatedAt => anime.LastUpdatedAt;

    IReadOnlyList<ICast> IWithCastAndCrew.Cast => anime.Cast;

    IReadOnlyList<ICrew> IWithCastAndCrew.Crew => anime.Crew;

    IAnidbAnime IAnidbSeason.Series => anime;

    IReadOnlyList<IAnidbEpisode> IAnidbSeason.Episodes => anime.Episodes
        .Where(x => x.Type == episodeType && x.SeasonNumber == seasonNumber)
        .ToList();

    IReadOnlyList<(int Year, YearlySeason Season)> IWithYearlySeasons.YearlySeasons
        => seasonNumber is 0 ? [] : anime.YearlySeasons;

    #region IWithImages Implementation

    public IImageCrossReference? DefaultPrimaryImageCrossReference => !string.IsNullOrEmpty(_imagePath) && IImageManager.GetIDForImageSourceAndResourceID(MetadataSource.AniDB, _imagePath) is { } posterID
        ? (this as IWithImages).GetImageCrossReferences(new() { ImageSource = MetadataSource.AniDB, ImageType = ImageEntityType.Primary }).FirstOrDefault(xref => xref.ImageID == posterID)
        : null;

    #endregion
}
