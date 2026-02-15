using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Stub;

#nullable enable
namespace Shoko.Server.Models.Shoko.Embedded;

public class AnimeSeason(IShokoSeries series, EpisodeType episodeType, int seasonNumber) : IShokoSeason
{
    int ISeason.SeriesID => series.ID;

    int ISeason.SeasonNumber => seasonNumber;

    IImage? ISeason.DefaultPoster
        => seasonNumber is 0 ? null : series.DefaultPoster;

    ISeries ISeason.Series => series;

    IReadOnlyList<IEpisode> ISeason.Episodes => series.Episodes
        .Where(x => x.Type == episodeType && x.SeasonNumber == seasonNumber)
        .ToList();

    string IWithTitles.Title
        => seasonNumber is 0
        ? "Specials"
        : series.Title;

    ITitle IWithTitles.DefaultTitle
        => seasonNumber is 0
            ? new TitleStub()
            {
                Language = TitleLanguage.English,
                LanguageCode = "en",
                Value = "Specials",
                Source = DataSource.Shoko,
                Type = TitleType.Official,
            }
            : series.DefaultTitle;

    ITitle? IWithTitles.PreferredTitle
        => seasonNumber is 0
            ? new TitleStub()
            {
                Language = TitleLanguage.English,
                LanguageCode = "en",
                Value = "Specials",
                Source = DataSource.Shoko,
                Type = TitleType.Official,
            }
            : series.PreferredTitle;

    IReadOnlyList<ITitle> IWithTitles.Titles => seasonNumber is 0
        ? [
            new TitleStub()
            {
                Language = TitleLanguage.English,
                LanguageCode = "en",
                Value = "Specials",
                Source = DataSource.Shoko,
                Type = TitleType.Official,
            },
        ]
        : series.Titles;

    IText? IWithDescriptions.DefaultDescription
        => seasonNumber is 0
            ? new TextStub()
            {
                Language = TitleLanguage.English,
                LanguageCode = "en",
                Value = "Specials",
                Source = DataSource.Shoko,
            }
            : series.DefaultDescription;

    IText? IWithDescriptions.PreferredDescription
        => seasonNumber is 0
            ? new TextStub()
            {
                Language = TitleLanguage.English,
                LanguageCode = "en",
                Value = "Specials",
                Source = DataSource.Shoko,
            }
            : series.PreferredDescription;

    IReadOnlyList<IText> IWithDescriptions.Descriptions => seasonNumber is 0
        ? [
            new TextStub()
            {
                Language = TitleLanguage.English,
                LanguageCode = "en",
                Value = "Specials",
                Source = DataSource.Shoko,
            },
        ]
        : series.Descriptions;

    DateTime IWithCreationDate.CreatedAt => series.CreatedAt;

    DateTime IWithUpdateDate.LastUpdatedAt => series.LastUpdatedAt;

    IReadOnlyList<ICast> IWithCastAndCrew.Cast => series.Cast;

    IReadOnlyList<ICrew> IWithCastAndCrew.Crew => series.Crew;

    string IMetadata<string>.ID => series.ID.ToString();

    DataSource IMetadata.Source => DataSource.AniDB;

    IShokoSeries IShokoSeason.Series => series;

    IReadOnlyList<IShokoEpisode> IShokoSeason.Episodes => series.Episodes
        .Where(x => x.Type == episodeType && x.SeasonNumber == seasonNumber)
        .ToList();

    IReadOnlyList<(int Year, YearlySeason Season)> IWithYearlySeasons.YearlySeasons
        => seasonNumber is 0 ? [] : series.YearlySeasons;

    IImage? IWithImages.GetPreferredImageForType(ImageEntityType entityType)
        => seasonNumber is 0 ? null : series.GetPreferredImageForType(entityType);

    IReadOnlyList<IImage> IWithImages.GetImages(ImageEntityType? entityType)
        => seasonNumber is 0 ? [] : series.GetImages(entityType);
}
