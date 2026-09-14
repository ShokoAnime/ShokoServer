using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anilist;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;

#nullable enable
namespace Shoko.Server.Models.Anilist.Embedded;

/// <summary>
/// Synthesized AniList season. AniList has no season entity and no specials,
/// so every anime has exactly one season wrapping its normal episodes, the
/// same way <see cref="AniDB.Embedded.AniDB_Season"/> wraps AniDB episodes.
/// </summary>
public class Anilist_Season(Anilist_Anime anime, int seasonNumber = 1) : IAnilistSeason
{
    public static string GetID(int anilistAnimeID, int seasonNumber) => $"{anilistAnimeID}:{seasonNumber}";

    public string ID => GetID(anime.AnilistAnimeID, seasonNumber);

    public Anilist_Anime Anime => anime;

    public int SeasonNumber => seasonNumber;

    public IReadOnlyList<Anilist_Episode> Episodes => anime.Episodes;

    #region IMetadata Implementation

    DataEntityType IMetadata.EntityType => DataEntityType.Season;

    DataSource IMetadata.Source => DataSource.AniList;

    #endregion

    #region ISeason Implementation

    int ISeason.SeriesID => anime.AnilistAnimeID;

    ISeries ISeason.Series => anime;

    IAnilistAnime IAnilistSeason.Series => anime;

    IReadOnlyList<IEpisode> ISeason.Episodes => Episodes;

    IReadOnlyList<IAnilistEpisode> IAnilistSeason.Episodes => Episodes;

    #endregion

    #region IWithTitles Implementation

    string IWithTitles.Title => ((IWithTitles)anime).Title;

    ITitle IWithTitles.DefaultTitle => ((IWithTitles)anime).DefaultTitle;

    ITitle? IWithTitles.PreferredTitle => ((IWithTitles)anime).PreferredTitle;

    IReadOnlyList<ITitle> IWithTitles.Titles => ((IWithTitles)anime).Titles;

    #endregion

    #region IWithDescriptions Implementation

    IText? IWithDescriptions.DefaultDescription => ((IWithDescriptions)anime).DefaultDescription;

    IText? IWithDescriptions.PreferredDescription => ((IWithDescriptions)anime).PreferredDescription;

    IReadOnlyList<IText> IWithDescriptions.Descriptions => ((IWithDescriptions)anime).Descriptions;

    #endregion

    #region IWithImages Implementation

    public IImageCrossReference? DefaultPrimaryImageCrossReference => !string.IsNullOrEmpty(anime.CoverImagePath) && IImageManager.GetIDForImageSourceAndResourceID(DataSource.AniList, anime.CoverImagePath) is { } imageID
        ? ((IWithImages)this).GetImageCrossReferences(new() { ImageSource = DataSource.AniList, ImageType = ImageEntityType.Primary }).FirstOrDefault(xref => xref.ImageID == imageID)
        : null;

    public IImageCrossReference? DefaultBannerImageCrossReference => !string.IsNullOrEmpty(anime.BannerImagePath) && IImageManager.GetIDForImageSourceAndResourceID(DataSource.AniList, anime.BannerImagePath) is { } imageID
        ? ((IWithImages)this).GetImageCrossReferences(new() { ImageSource = DataSource.AniList, ImageType = ImageEntityType.Banner }).FirstOrDefault(xref => xref.ImageID == imageID)
        : null;

    #endregion

    #region IWithCastAndCrew Implementation

    IReadOnlyList<ICast> IWithCastAndCrew.Cast => ((IWithCastAndCrew)anime).Cast;

    IReadOnlyList<ICrew> IWithCastAndCrew.Crew => ((IWithCastAndCrew)anime).Crew;

    #endregion

    #region IWithYearlySeasons Implementation

    IReadOnlyList<(int Year, YearlySeason Season)> IWithYearlySeasons.YearlySeasons => ((IWithYearlySeasons)anime).YearlySeasons;

    #endregion

    #region IWithUpdateDate Implementation

    DateTime IWithUpdateDate.LastUpdatedAt => anime.LastUpdatedAt;

    #endregion
}
