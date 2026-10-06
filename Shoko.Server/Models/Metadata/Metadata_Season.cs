using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Server.Models.Metadata.Embedded;
using Shoko.Server.Repositories;
using Shoko.Server.Services;
using Shoko.Server.Utilities;

namespace Shoko.Server.Models.Metadata;

/// <summary>
///   A season a plugin source keeps in the series store, as part of its
///   series.
/// </summary>
public class Metadata_Season : ISeason<ISeries, IEpisode>, IMetadataStoreRow<Metadata_Season>, IMetadataDefaultImageSource
{
    #region Database Columns

    /// <summary>
    ///   The row's ID.
    /// </summary>
    public int Metadata_SeasonID { get; set; }

    /// <summary>
    ///   The source the season belongs to.
    /// </summary>
    public MetadataSource Source { get; set; } = null!;

    /// <summary>
    ///   The source's own ID for the season.
    /// </summary>
    public string ProviderID { get; set; } = string.Empty;

    /// <summary>
    ///   The source's own ID for the series the season belongs to.
    /// </summary>
    public string SeriesID { get; set; } = string.Empty;

    /// <summary>
    ///   The season's number in the series' own ordering.
    /// </summary>
    public int SeasonNumber { get; set; }

    /// <summary>
    ///   When the store first wrote the season. Set once and never changed.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    ///   When the source last wrote the season.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    /// <summary>
    ///   What the source said of the season that needs no column of its
    ///   own, or <c>null</c> when it said none of it.
    /// </summary>
    public Metadata_SeasonExtra? ExtraData { get; set; }

    #endregion

    #region Helpers

    /// <summary>
    ///   The season's identifier.
    /// </summary>
    public MetadataGuid ID => new(Source, MetadataEntityType.Season, ProviderID);

    /// <summary>
    ///   Whether the stored columns are the same as another row's, leaving
    ///   out the row's ID and when it was written.
    /// </summary>
    /// <param name="other">The other row.</param>
    /// <returns><c>true</c> when nothing but the ID and time differ.</returns>
    internal bool SameAs(Metadata_Season other)
        => Source == other.Source &&
            ProviderID == other.ProviderID &&
            SeriesID == other.SeriesID &&
            SeasonNumber == other.SeasonNumber &&
            Equals(ExtraData, other.ExtraData);

    /// <summary>
    ///   The season's episodes, by type and number.
    /// </summary>
    public IReadOnlyList<Metadata_Episode> StoredEpisodes
        => [.. Metadata_Episode.InOrder(RepoFactory.Metadata_Episode.GetBySeasonID(Source, ProviderID))];

    #endregion

    #region Store Row Implementation

    int IMetadataStoreRow<Metadata_Season>.RowID
    {
        get => Metadata_SeasonID;
        set => Metadata_SeasonID = value;
    }

    Metadata_Season IMetadataStoreRow<Metadata_Season>.Clone()
        => (Metadata_Season)MemberwiseClone();

    #endregion

    #region IWithTitles Implementation

    string IWithTitles.Title => ((IWithTitles)this).PreferredTitle?.Value ?? ((IWithTitles)this).DefaultTitle.Value;

    ITitle IWithTitles.DefaultTitle => MetadataStoredEntry.DefaultTitle(this);

    ITitle? IWithTitles.PreferredTitle => MetadataStoredEntry.PreferredTitle(this);

    IReadOnlyList<ITitle> IWithTitles.Titles => MetadataStoredEntry.Titles(this);

    #endregion

    #region IWithOverviews Implementation

    IText? IWithOverviews.DefaultOverview => MetadataStoredEntry.DefaultOverview(this);

    IText? IWithOverviews.PreferredOverview => MetadataStoredEntry.PreferredOverview(this);

    IReadOnlyList<IText> IWithOverviews.Overviews => MetadataStoredEntry.Overviews(this);

    #endregion

    #region IMetadataDefaultImageSource Implementation

    string? IMetadataDefaultImageSource.GetDefaultResourceID(ImageEntityType imageType)
        => ExtraData?.GetDefaultResourceID(imageType);

    #endregion

    #region IWithImages Implementation

    IImageCrossReference? IWithImages.DefaultPrimaryImageCrossReference => MetadataStoredEntry.DefaultImage(this, ImageEntityType.Primary);

    IImageCrossReference? IWithImages.DefaultBackdropImageCrossReference => MetadataStoredEntry.DefaultImage(this, ImageEntityType.Backdrop);

    IImageCrossReference? IWithImages.DefaultLogoImageCrossReference => MetadataStoredEntry.DefaultImage(this, ImageEntityType.Logo);

    IImageCrossReference? IWithImages.DefaultBannerImageCrossReference => MetadataStoredEntry.DefaultImage(this, ImageEntityType.Banner);

    IImageCrossReference? IWithImages.DefaultDiscImageCrossReference => MetadataStoredEntry.DefaultImage(this, ImageEntityType.Disc);

    #endregion

    #region IWithCastAndCrew Implementation

    IReadOnlyList<ICast> IWithCastAndCrew.Cast => MetadataStoredEntry.Cast(ID);

    IReadOnlyList<ICrew> IWithCastAndCrew.Crew => MetadataStoredEntry.Crew(ID);

    #endregion

    #region IWithYearlySeasons Implementation

    IReadOnlyList<(int Year, YearlySeason Season)> IWithYearlySeasons.YearlySeasons
    {
        get
        {
            var aired = StoredEpisodes.Select(episode => episode.AirDate).OfType<DateOnly>();
            var series = RepoFactory.Metadata_Series.GetByProviderID(Source, SeriesID);
            return SeasonCalendar.GetSeasons(SeasonCalendar.GetSpan(series?.Type ?? AnimeType.TV, aired, null, series?.EndDate));
        }
    }

    #endregion

    #region ISeason Implementation

    DateTime? ISeason.LastRefreshedAt => RepoFactory.Metadata_Series.GetByProviderID(Source, SeriesID)?.LastRefreshedAt?.ToUniversalTime();

    MetadataGuid ISeason.SeriesID => new(Source, MetadataEntityType.Series, SeriesID);

    ISeries ISeason<ISeries, IEpisode>.Series => RepoFactory.Metadata_Series.GetByProviderID(Source, SeriesID) ??
        throw new NullReferenceException($"Unable to find {Source.Name} series {SeriesID} for its season {ProviderID}");

    IReadOnlyList<IEpisode> ISeason<ISeries, IEpisode>.Episodes => StoredEpisodes;

    ISeries ISeason.Series => ((ISeason<ISeries, IEpisode>)this).Series;

    IReadOnlyList<IEpisode> ISeason.Episodes => ((ISeason<ISeries, IEpisode>)this).Episodes;

    IOrdering<ISeries, IEpisode> ISeason<ISeries, IEpisode>.Ordering => OrderingLookup.DefaultFor<ISeries, IEpisode>(((ISeason<ISeries, IEpisode>)this).Series);

    IReadOnlyList<IMetadataSeasonCrossReference> ISeason.MetadataSeasonCrossReferences
        => MetadataService.GetSeasonCrossReferences(this, ((ISeason)this).MetadataEpisodeCrossReferences);

    IReadOnlyList<IMetadataEpisodeCrossReference> ISeason.MetadataEpisodeCrossReferences
        => MetadataStoredEntry.EpisodeLinksTo(StoredEpisodes.Select(episode => episode.ID));

    // A film sits in no season, so nothing links one to a provider's season.
    IReadOnlyList<IMetadataMovieCrossReference> ISeason.MetadataMovieCrossReferences => [];

    #endregion
}
