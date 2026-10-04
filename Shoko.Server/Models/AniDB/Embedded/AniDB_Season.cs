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
using Shoko.Server.Models.Metadata.Embedded;
using Shoko.Server.Services;

namespace Shoko.Server.Models.AniDB.Embedded;

public class AniDB_Season(IAnidbAnime anime, EpisodeType episodeType, int seasonNumber) : ISeason<IAnidbAnime, IAnidbEpisode>
{
    public static string GetID(int animeID, EpisodeType episodeType, int seasonNumber) => $"{animeID}:{episodeType}:{seasonNumber}";

    MetadataGuid IMetadata.ID => new(MetadataSource.AniDB, MetadataEntityType.Season, GetID(anime.AnidbID, episodeType, seasonNumber));

    private readonly string? _imagePath = ((AniDB_Anime)anime).Picname;

    MetadataGuid ISeason.SeriesID => anime.ID;

    int ISeason.SeasonNumber => seasonNumber;

    IAnidbAnime ISeason<IAnidbAnime, IAnidbEpisode>.Series => anime;

    IReadOnlyList<IAnidbEpisode> ISeason<IAnidbAnime, IAnidbEpisode>.Episodes => [.. Episodes];

    ISeries ISeason.Series => ((ISeason<IAnidbAnime, IAnidbEpisode>)this).Series;

    IReadOnlyList<IEpisode> ISeason.Episodes => ((ISeason<IAnidbAnime, IAnidbEpisode>)this).Episodes;

    IOrdering<IAnidbAnime, IAnidbEpisode> ISeason<IAnidbAnime, IAnidbEpisode>.Ordering => OrderingLookup.DefaultFor<IAnidbAnime, IAnidbEpisode>(anime);

    /// <summary>
    ///   The season's episodes.
    /// </summary>
    private IEnumerable<IAnidbEpisode> Episodes
        => ((ISeries<IAnidbAnime, IAnidbEpisode>)anime).Episodes.Where(x => x.Type == episodeType && x.SeasonNumber == seasonNumber);

    IReadOnlyList<IMetadataSeasonCrossReference> ISeason.MetadataSeasonCrossReferences
        => MetadataService.GetSeasonCrossReferences(this, ((ISeason)this).MetadataEpisodeCrossReferences);

    IReadOnlyList<IMetadataEpisodeCrossReference> ISeason.MetadataEpisodeCrossReferences
        => [.. Episodes.SelectMany(episode => episode.MetadataEpisodeCrossReferences)];

    IReadOnlyList<IMetadataMovieCrossReference> ISeason.MetadataMovieCrossReferences
        => [.. Episodes.SelectMany(episode => episode.MetadataMovieCrossReferences)];

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

    DateTime IWithCreationDate.CreatedAt => anime.CreatedAt;

    DateTime IWithUpdateDate.LastUpdatedAt => anime.LastUpdatedAt;

    IReadOnlyList<ICast> IWithCastAndCrew.Cast => anime.Cast;

    IReadOnlyList<ICrew> IWithCastAndCrew.Crew => anime.Crew;

    IReadOnlyList<(int Year, YearlySeason Season)> IWithYearlySeasons.YearlySeasons
        => seasonNumber is 0 ? [] : anime.YearlySeasons;

    #region IWithImages Implementation

    public IImageCrossReference? DefaultPrimaryImageCrossReference => !string.IsNullOrEmpty(_imagePath) && IImageManager.GetIDForImageSourceAndResourceID(MetadataSource.AniDB, _imagePath) is { } posterID
        ? (this as IWithImages).GetImageCrossReferences(new() { ImageSource = MetadataSource.AniDB, ImageType = ImageEntityType.Primary }).FirstOrDefault(xref => xref.ImageID == posterID)
        : null;

    #endregion
}
