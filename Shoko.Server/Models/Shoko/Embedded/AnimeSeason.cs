using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Abstractions.Metadata.Tmdb.CrossReferences;

namespace Shoko.Server.Models.Shoko.Embedded;

public class AnimeSeason(IShokoSeries series, EpisodeType episodeType, int seasonNumber) : IShokoSeason
{
    int ISeason.SeriesID => series.ID;

    int ISeason.SeasonNumber => seasonNumber;

    public IImageCrossReference? DefaultPrimaryImageCrossReference
        => series.DefaultBackdropImageCrossReference;

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
            ? new TitleStub
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
            ? new TitleStub
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
            new TitleStub
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
            ? new TextStub
            {
                Language = TitleLanguage.English,
                LanguageCode = "en",
                Value = "Specials",
                Source = DataSource.Shoko,
            }
            : series.DefaultDescription;

    IText? IWithDescriptions.PreferredDescription
        => seasonNumber is 0
            ? new TextStub
            {
                Language = TitleLanguage.English,
                LanguageCode = "en",
                Value = "Specials",
                Source = DataSource.Shoko,
            }
            : series.PreferredDescription;

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
        : series.Descriptions;

    DateTime IWithCreationDate.CreatedAt => series.CreatedAt;

    DateTime IWithUpdateDate.LastUpdatedAt => series.LastUpdatedAt;

    IReadOnlyList<ICast> IWithCastAndCrew.Cast => series.Cast;

    IReadOnlyList<ICrew> IWithCastAndCrew.Crew => series.Crew;

    string IMetadata<string>.ID => $"{series.ID}:{episodeType}:{seasonNumber}";

    DataEntityType IMetadata.EntityType => DataEntityType.Season;

    DataSource IMetadata.Source => DataSource.AniDB;

    IShokoSeries IShokoSeason.Series => series;

    IReadOnlyList<IShokoEpisode> IShokoSeason.Episodes => series.Episodes
        .Where(x => x.Type == episodeType && x.SeasonNumber == seasonNumber)
        .ToList();

    IReadOnlyList<ITmdbSeason> IShokoSeason.TmdbSeasons => series.Episodes
        .Where(x => x.Type == episodeType && x.SeasonNumber != seasonNumber)
        .OfType<AnimeEpisode>()
        .SelectMany(x => x.TmdbEpisodeCrossReferences)
        .Select(xref => xref.TmdbSeason)
        .WhereNotNull()
        .DistinctBy(xref => xref.TmdbSeasonID)
        .ToList();

    IReadOnlyList<ITmdbMovie> IShokoSeason.TmdbMovies => series.Episodes
        .Where(x => x.Type == episodeType && x.SeasonNumber == seasonNumber)
        .OfType<AnimeEpisode>()
        .SelectMany(x => x.TmdbMovies)
        .ToList();

    IReadOnlyList<ITmdbSeasonCrossReference> IShokoSeason.TmdbSeasonCrossReferences => series.Episodes
        .Where(x => x.Type == episodeType && x.SeasonNumber == seasonNumber)
        .OfType<AnimeEpisode>()
        .SelectMany(x => x.TmdbEpisodeCrossReferences)
        .Select(xref => xref.TmdbSeasonCrossReference)
        .WhereNotNull()
        .DistinctBy(xref => xref.TmdbSeasonID)
        .ToList();

    IReadOnlyList<ITmdbEpisodeCrossReference> IShokoSeason.TmdbEpisodeCrossReferences => series.Episodes
        .Where(x => x.Type == episodeType && x.SeasonNumber == seasonNumber)
        .OfType<AnimeEpisode>()
        .SelectMany(x => x.TmdbEpisodeCrossReferences)
        .ToList();

    IReadOnlyList<ITmdbMovieCrossReference> IShokoSeason.TmdbMovieCrossReferences => series.Episodes
        .Where(x => x.Type == episodeType && x.SeasonNumber == seasonNumber)
        .OfType<AnimeEpisode>()
        .SelectMany(x => x.TmdbMovieCrossReferences)
        .ToList();

    IReadOnlyList<ISeason> IShokoSeason.LinkedSeasons => series.Episodes
        .Where(x => x.Type == episodeType && x.SeasonNumber != seasonNumber)
        .OfType<AnimeEpisode>()
        .SelectMany(x => x.TmdbEpisodeCrossReferences)
        .Select(xref => xref.TmdbSeason)
        .WhereNotNull()
        .DistinctBy(xref => xref.TmdbSeasonID)
        .ToList();

    IReadOnlyList<(int Year, YearlySeason Season)> IWithYearlySeasons.YearlySeasons
        => seasonNumber is 0 ? [] : series.YearlySeasons;
}
