using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Models.Metadata.Embedded;
using Shoko.Server.Services;

#pragma warning disable CS0618
namespace Shoko.Server.Models.Shoko.Embedded;

public class AnimeSeason(IShokoSeries series, EpisodeType episodeType, int seasonNumber) : ISeason<IShokoSeries, IShokoEpisode>, IWithCreationDate
{
    /// <summary>
    ///   The ID of a season of a Shoko series, the part after
    ///   <c>shoko://season/</c> in its <see cref="MetadataGuid"/>.
    /// </summary>
    /// <param name="seriesID">The Shoko series ID.</param>
    /// <param name="episodeType">The type of the season's episodes.</param>
    /// <param name="seasonNumber">The season number.</param>
    /// <returns>The season's ID.</returns>
    public static string GetID(int seriesID, EpisodeType episodeType, int seasonNumber) => $"{seriesID}:{episodeType}:{seasonNumber}";

    MetadataGuid ISeason.SeriesID => series.ID;

    int ISeason.SeasonNumber => seasonNumber;

    public IImageCrossReference? DefaultPrimaryImageCrossReference
        => series.DefaultBackdropImageCrossReference;

    IShokoSeries ISeason<IShokoSeries, IShokoEpisode>.Series => series;

    IReadOnlyList<IShokoEpisode> ISeason<IShokoSeries, IShokoEpisode>.Episodes => [.. Episodes];

    ISeries ISeason.Series => ((ISeason<IShokoSeries, IShokoEpisode>)this).Series;

    IReadOnlyList<IEpisode> ISeason.Episodes => ((ISeason<IShokoSeries, IShokoEpisode>)this).Episodes;

    IOrdering<IShokoSeries, IShokoEpisode> ISeason<IShokoSeries, IShokoEpisode>.Ordering => OrderingLookup.DefaultFor<IShokoSeries, IShokoEpisode>(series);

    /// <summary>
    ///   The season's episodes.
    /// </summary>
    private IEnumerable<IShokoEpisode> Episodes
        => ((ISeries<IShokoSeries, IShokoEpisode>)series).Episodes.Where(x => x.Type == episodeType && x.SeasonNumber == seasonNumber);

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
                Source = MetadataSource.Shoko,
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
                Source = MetadataSource.Shoko,
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
                Source = MetadataSource.Shoko,
                Type = TitleType.Official,
            },
        ]
        // A season borrows the series' titles when nothing names it, so a
        // provider that does name its seasons goes first.
        : [.. ProviderTitles, .. series.Titles];

    /// <summary>
    ///   What the season's own entries call it, empty when nothing does.
    /// </summary>
    private IReadOnlyList<ITitle> ProviderTitles
        => ISystemService.StaticServices.GetRequiredService<IMetadataTextManager>() is MetadataTextManager textManager
            ? textManager.SeasonTitlesOf(this)
            : [];

    /// <summary>
    ///   What the season's own entries say about it, empty when nothing does.
    /// </summary>
    private IReadOnlyList<IText> ProviderDescriptions
        => ISystemService.StaticServices.GetRequiredService<IMetadataTextManager>() is MetadataTextManager textManager
            ? textManager.SeasonDescriptionsOf(this)
            : [];

    IText? IWithOverviews.DefaultOverview
        => seasonNumber is 0
            ? new TextStub
            {
                Language = TitleLanguage.English,
                LanguageCode = "en",
                Value = "Specials",
                Source = MetadataSource.Shoko,
            }
            : series.DefaultOverview;

    IText? IWithOverviews.PreferredOverview
        => seasonNumber is 0
            ? new TextStub
            {
                Language = TitleLanguage.English,
                LanguageCode = "en",
                Value = "Specials",
                Source = MetadataSource.Shoko,
            }
            : series.PreferredOverview;

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
        : [.. ProviderDescriptions, .. series.Overviews];

    DateTime IWithCreationDate.CreatedAt => series.CreatedAt;

    DateTime IWithUpdateDate.LastUpdatedAt => series.LastUpdatedAt;

    IReadOnlyList<ICast> IWithCastAndCrew.Cast => series.Cast;

    IReadOnlyList<ICrew> IWithCastAndCrew.Crew => series.Crew;

    MetadataGuid IMetadata.ID => new(MetadataSource.Shoko, MetadataEntityType.Season, GetID(series.LocalID, episodeType, seasonNumber));

    IReadOnlyList<IMetadataSeasonCrossReference> ISeason.MetadataSeasonCrossReferences
        => MetadataService.GetSeasonCrossReferences(this, ((ISeason)this).MetadataEpisodeCrossReferences);

    IReadOnlyList<IMetadataEpisodeCrossReference> ISeason.MetadataEpisodeCrossReferences
        => [.. Episodes.SelectMany(episode => episode.MetadataEpisodeCrossReferences)];

    IReadOnlyList<IMetadataMovieCrossReference> ISeason.MetadataMovieCrossReferences
        => [.. Episodes.SelectMany(episode => episode.MetadataMovieCrossReferences)];

    IReadOnlyList<(int Year, YearlySeason Season)> IWithYearlySeasons.YearlySeasons
        => seasonNumber is 0 ? [] : series.YearlySeasons;
}
