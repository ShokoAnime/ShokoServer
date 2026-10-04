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
using Shoko.Abstractions.Video;
using Shoko.Server.Models.Metadata.Embedded;
using Shoko.Server.Repositories;

#pragma warning disable CS0618
namespace Shoko.Server.Models.Metadata;

/// <summary>
///   An episode a plugin source keeps in the series store, as part of its
///   series.
/// </summary>
public class Metadata_Episode : IEpisode<ISeries, IEpisode>, IMetadataStoreRow<Metadata_Episode>, IMetadataDefaultImageSource
{
    #region Database Columns

    /// <summary>
    ///   The row's ID.
    /// </summary>
    public int Metadata_EpisodeID { get; set; }

    /// <summary>
    ///   The source the episode belongs to.
    /// </summary>
    public MetadataSource Source { get; set; } = null!;

    /// <summary>
    ///   The source's own ID for the episode.
    /// </summary>
    public string ProviderID { get; set; } = string.Empty;

    /// <summary>
    ///   The source's own ID for the series the episode belongs to.
    /// </summary>
    public string SeriesID { get; set; } = string.Empty;

    /// <summary>
    ///   The source's own ID for the season the episode sits in, or
    ///   <c>null</c> for none.
    /// </summary>
    public string? SeasonID { get; set; }

    /// <summary>
    ///   The season's number, when the episode sits in one.
    /// </summary>
    public int? SeasonNumber { get; set; }

    /// <summary>
    ///   The episode's number, within its season when it has one.
    /// </summary>
    public int EpisodeNumber { get; set; }

    /// <summary>
    ///   What kind of episode it is.
    /// </summary>
    public EpisodeType Type { get; set; } = EpisodeType.Episode;

    /// <summary>
    ///   The source's user rating, on a scale of 1 to 10.
    /// </summary>
    public double Rating { get; set; }

    /// <summary>
    ///   How many votes the rating is made from.
    /// </summary>
    public int RatingVotes { get; set; }

    /// <summary>
    ///   How long the episode runs, in whole seconds, or <c>0</c> when not
    ///   known.
    /// </summary>
    public int RuntimeSeconds { get; set; }

    /// <summary>
    ///   The day the episode aired, when known.
    /// </summary>
    public DateOnly? AirDate { get; set; }

    /// <summary>
    ///   The precise day and time the episode aired, in UTC, when known.
    /// </summary>
    public DateTime? AirDateWithTime { get; set; }

    /// <summary>
    ///   The links to the episode elsewhere that its source gave.
    /// </summary>
    public List<Resource> Resources { get; set; } = [];

    /// <summary>
    ///   The IDs other sources gave the same episode, as its source listed
    ///   them.
    /// </summary>
    public List<MetadataGuid> CrossSourceIDs { get; set; } = [];

    /// <summary>
    ///   What the source said of the episode that needs no column of its
    ///   own, or <c>null</c> when it said none of it.
    /// </summary>
    public Metadata_EpisodeExtra? ExtraData { get; set; }

    /// <summary>
    ///   When the store first wrote the episode. Set once and never changed.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    ///   When the source last wrote the episode.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    /// <summary>
    ///   Whether a user hid the episode. A save of its series that keeps the
    ///   episode keeps it; set through
    ///   <see cref="IMetadataOrderingService.SetEpisodeHidden"/>.
    /// </summary>
    public bool IsHidden { get; set; }

    #endregion

    #region Helpers

    /// <summary>
    ///   The episode's identifier.
    /// </summary>
    public MetadataGuid ID => new(Source, MetadataEntityType.Episode, ProviderID);

    /// <summary>
    ///   The season of the regular episode a special airs before, as its
    ///   source said. Always <c>null</c> outside season 0.
    /// </summary>
    internal int? AirsBeforeSeasonNumber => ExtraData?.AirsBeforeSeasonNumber;

    /// <summary>
    ///   The number of the regular episode a special airs before, as its
    ///   source said. Always <c>null</c> outside season 0.
    /// </summary>
    internal int? AirsBeforeEpisodeNumber => ExtraData?.AirsBeforeEpisodeNumber;

    /// <summary>
    ///   The season a special airs after, as its source said. Always
    ///   <c>null</c> outside season 0.
    /// </summary>
    internal int? AirsAfterSeasonNumber => ExtraData?.AirsAfterSeasonNumber;

    /// <summary>
    ///   Whether the stored columns are the same as another row's, leaving
    ///   out the row's ID and when it was written.
    /// </summary>
    /// <param name="other">The other row.</param>
    /// <returns><c>true</c> when nothing but the ID and time differ.</returns>
    internal bool SameAs(Metadata_Episode other)
        => Source == other.Source &&
            ProviderID == other.ProviderID &&
            SeriesID == other.SeriesID &&
            SeasonID == other.SeasonID &&
            SeasonNumber == other.SeasonNumber &&
            EpisodeNumber == other.EpisodeNumber &&
            Type == other.Type &&
            Rating.Equals(other.Rating) &&
            RatingVotes == other.RatingVotes &&
            RuntimeSeconds == other.RuntimeSeconds &&
            AirDate == other.AirDate &&
            AirDateWithTime == other.AirDateWithTime &&
            MetadataStoredEntry.SameResources(Resources, other.Resources) &&
            CrossSourceIDs.SequenceEqual(other.CrossSourceIDs) &&
            Equals(ExtraData, other.ExtraData);

    /// <summary>
    ///   Puts episodes in a stable order: by season number, type, number and
    ///   ID.
    /// </summary>
    /// <param name="episodes">The episodes.</param>
    /// <returns>The episodes, in order.</returns>
    internal static IEnumerable<Metadata_Episode> InOrder(IEnumerable<Metadata_Episode> episodes)
        => episodes
            .OrderBy(episode => episode.SeasonNumber ?? int.MaxValue)
            .ThenBy(episode => episode.Type)
            .ThenBy(episode => episode.EpisodeNumber)
            .ThenBy(episode => episode.ProviderID, StringComparer.Ordinal);

    /// <summary>
    ///   The links naming the episode.
    /// </summary>
    private IReadOnlyList<IMetadataEpisodeCrossReference> Links
        => MetadataStoredEntry.EpisodeLinksTo([ID]);

    #endregion

    #region Store Row Implementation

    int IMetadataStoreRow<Metadata_Episode>.RowID
    {
        get => Metadata_EpisodeID;
        set => Metadata_EpisodeID = value;
    }

    Metadata_Episode IMetadataStoreRow<Metadata_Episode>.Clone()
        => (Metadata_Episode)MemberwiseClone();

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

    #endregion

    #region IWithCastAndCrew Implementation

    IReadOnlyList<ICast> IWithCastAndCrew.Cast => MetadataStoredEntry.Cast(ID);

    IReadOnlyList<ICrew> IWithCastAndCrew.Crew => MetadataStoredEntry.Crew(ID);

    #endregion

    #region IWithResources Implementation

    IReadOnlyList<Resource> IWithResources.Resources
        => [.. Resources, .. ISystemService.StaticServices.GetRequiredService<IMetadataService>().GatherResourcesForEntity(this)];

    #endregion

    #region IWithCrossSources Implementation

    IReadOnlyList<MetadataGuid> IWithCrossSources.CrossSourceIDs => CrossSourceIDs;

    #endregion

    #region IEpisode Implementation

    DateTime? IEpisode.LastRefreshedAt => RepoFactory.Metadata_Series.GetByProviderID(Source, SeriesID)?.LastRefreshedAt?.ToUniversalTime();

    IReadOnlyList<IEpisodeOrderingInformation<ISeries, IEpisode>> IEpisode<ISeries, IEpisode>.Orderings => OrderingLookup.PlacesOf<ISeries, IEpisode>(this);

    IEpisodeOrderingInformation<ISeries, IEpisode>? IEpisode<ISeries, IEpisode>.PreferredOrdering => OrderingLookup.PreferredPlaceOf<ISeries, IEpisode>(this);

    IEpisodeOrderingInformation<ISeries, IEpisode> IEpisode<ISeries, IEpisode>.CurrentOrdering => OrderingLookup.DefaultPlaceOf<ISeries, IEpisode>(this);

    ISeries IEpisode.Series => ((IEpisode<ISeries, IEpisode>)this).Series;

    ISeason? IEpisode.Season => ((IEpisode<ISeries, IEpisode>)this).Season;

    IReadOnlyList<IEpisodeOrderingInformation> IEpisode.Orderings => ((IEpisode<ISeries, IEpisode>)this).Orderings;

    IEpisodeOrderingInformation? IEpisode.PreferredOrdering => ((IEpisode<ISeries, IEpisode>)this).PreferredOrdering;

    IEpisodeOrderingInformation IEpisode.CurrentOrdering => ((IEpisode<ISeries, IEpisode>)this).CurrentOrdering;

    MetadataGuid IEpisode.SeriesID => new(Source, MetadataEntityType.Series, SeriesID);

    MetadataGuid? IEpisode.SeasonID => string.IsNullOrEmpty(SeasonID) ? null : new(Source, MetadataEntityType.Season, SeasonID);

    IReadOnlyList<int> IEpisode.ShokoEpisodeIDs => [.. ((IEpisode)this).ShokoEpisodes.Select(episode => episode.LocalID)];

    TimeSpan IEpisode.Runtime => TimeSpan.FromSeconds(RuntimeSeconds);

    ISeries IEpisode<ISeries, IEpisode>.Series => RepoFactory.Metadata_Series.GetByProviderID(Source, SeriesID) ??
        throw new NullReferenceException($"Unable to find {Source.Name} series {SeriesID} for its episode {ProviderID}");

    ISeason<ISeries, IEpisode>? IEpisode<ISeries, IEpisode>.Season => string.IsNullOrEmpty(SeasonID) ? null : RepoFactory.Metadata_Season.GetByProviderID(Source, SeasonID);

    IReadOnlyList<IShokoEpisode> IEpisode.ShokoEpisodes => MetadataStoredEntry.ShokoEpisodes(Links.Select(link => link.AnidbEpisodeID));

    IReadOnlyList<IVideoCrossReference> IEpisode.VideoCrossReferences
        => MetadataStoredEntry.VideoLinksForEpisodes(Links.Select(link => link.AnidbEpisodeID));

    IReadOnlyList<IMetadataEpisodeCrossReference> IEpisode.MetadataEpisodeCrossReferences => Links;

    IReadOnlyList<IMetadataSeriesCrossReference> IEpisode.MetadataSeriesCrossReferences
        => MetadataStoredEntry.SeriesLinksTo(new(Source, MetadataEntityType.Series, SeriesID));

    // A film claims no episode of a provider's, only the AniDB one standing for it.
    IReadOnlyList<IMetadataMovieCrossReference> IEpisode.MetadataMovieCrossReferences => [];

    IReadOnlyList<IVideo> IEpisode.Videos => MetadataStoredEntry.Videos(((IEpisode)this).VideoCrossReferences);

    #endregion
}
