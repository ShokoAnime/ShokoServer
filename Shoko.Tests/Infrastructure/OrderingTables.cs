using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Image.Options;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Services;
using Shoko.Server.Settings;

namespace Shoko.Tests.Infrastructure;

/// <summary>
/// The ordering service's tables in memory, and a way to build the service
/// over them.
/// </summary>
/// <remarks>
/// Rows write through <see cref="CacheOnlyRowWriter"/>, the chosen orderings
/// and hidden flags live in <see cref="RowState"/>, and the image links in
/// <see cref="ImageLinks"/>. Hiding a Shoko episode whose series is present
/// would update the series and group stats, which these tables can't do, so a
/// test hides only Shoko episodes whose series is not there.
/// </remarks>
public sealed class OrderingTables
{
    public CacheOnlyRowWriter Writer { get; } = new();

    /// <summary>
    /// The stored texts, which the orderings' writes tell about.
    /// </summary>
    public TextCache Texts { get; } = new();

    /// <summary>
    /// The text store the service writes through; one over <see cref="Texts"/>
    /// unless a test shares its own.
    /// </summary>
    public MetadataTextStore TextStore { get; set; }

    public Metadata_OrderingRepository Orderings { get; }
        = CachedRepo.Build<Metadata_OrderingRepository, int, Metadata_Ordering>(row => row.Metadata_OrderingID);

    public Metadata_Ordering_GroupRepository Groups { get; }
        = CachedRepo.Build<Metadata_Ordering_GroupRepository, int, Metadata_Ordering_Group>(row => row.Metadata_Ordering_GroupID);

    public Metadata_Ordering_EntryRepository Entries { get; }
        = CachedRepo.Build<Metadata_Ordering_EntryRepository, int, Metadata_Ordering_Entry>(row => row.Metadata_Ordering_EntryID);

    public Metadata_NetworkRepository Networks { get; }
        = CachedRepo.Build<Metadata_NetworkRepository, int, Metadata_Network>(row => row.Metadata_NetworkID);

    public Metadata_Network_EntryRepository NetworkEntries { get; }
        = CachedRepo.Build<Metadata_Network_EntryRepository, int, Metadata_Network_Entry>(row => row.Metadata_Network_EntryID);

    /// <summary>
    /// The studio store the orderings' networks go through; one over
    /// <see cref="Networks"/> and <see cref="NetworkEntries"/>, writing apart
    /// from <see cref="Writer"/>, unless a test shares its own.
    /// </summary>
    public MetadataStudioStore StudioStore { get; set; }

    /// <summary>
    /// The chosen orderings and hidden flags on the rows of the series and
    /// episodes the metadata service finds.
    /// </summary>
    public FakeOrderingRowState RowState { get; } = new();

    public Mock<AnimeEpisodeRepository> ShokoEpisodes { get; }

    /// <summary>
    /// The image links the image manager holds, by entity.
    /// </summary>
    public List<IImageCrossReference> ImageLinks { get; } = [];

    /// <summary>
    /// The image manager the removal of image links goes through.
    /// </summary>
    public Mock<IImageManager> Images { get; } = new();

    /// <summary>
    /// The settings the service reads, such as whether AniDB specials are
    /// placed by their titles.
    /// </summary>
    public ServerSettings Settings { get; } = new();

    /// <summary>
    /// Builds the tables.
    /// </summary>
    /// <param name="shokoEpisodes">The Shoko episodes the hidden state reads, if any.</param>
    public OrderingTables(params AnimeEpisode[] shokoEpisodes)
    {
        TextStore = new(Texts, Writer);
        StudioStore = new MetadataStudioStore(
            CachedRepo.Build<Metadata_StudioRepository, int, Metadata_Studio>(row => row.Metadata_StudioID),
            CachedRepo.Build<Metadata_Studio_EntryRepository, int, Metadata_Studio_Entry>(row => row.Metadata_Studio_EntryID),
            Networks,
            NetworkEntries,
            new CacheOnlyRowWriter(),
            TextStore
        );
        ShokoEpisodes = CachedRepo.BuildWritable<AnimeEpisodeRepository, int, AnimeEpisode>(episode => episode.AnimeEpisodeID, shokoEpisodes);
        ShokoEpisodes.Setup(repository => repository.Save(It.IsAny<AnimeEpisode>()))
            .Callback<AnimeEpisode>(episode => ShokoEpisodes.Object.Cache.Update(episode));
        Images.Setup(images => images.GetImageCrossReferencesForEntity(It.IsAny<IWithImages>(), It.IsAny<ImageCrossReferenceFilteringOptions?>()))
            .Returns((IWithImages entity, ImageCrossReferenceFilteringOptions? _) => [.. ImageLinks.Where(link => link.EntityID == entity.ID)]);
        Images.Setup(images => images.RemoveImageCrossReference(It.IsAny<IImageCrossReference>()))
            .Returns((IImageCrossReference link) => ImageLinks.Remove(link));
    }

    /// <summary>
    /// Links an image to an entity.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <returns>The link.</returns>
    public IImageCrossReference AddImageLink(MetadataGuid entity)
    {
        var link = new Mock<IImageCrossReference>();
        link.SetupGet(value => value.EntityID).Returns(entity);
        ImageLinks.Add(link.Object);
        return link.Object;
    }

    /// <summary>
    /// Builds the service over the tables.
    /// </summary>
    /// <param name="metadata">Finds the series and episodes, called on first use.</param>
    /// <returns>The service.</returns>
    public MetadataOrderingService Build(Func<IMetadataService> metadata)
    {
        RowState.SeriesExists = id => metadata().GetSeries(id) is not null;
        RowState.EpisodeExists = id => metadata().GetEpisode(id) is not null;
        return Build(metadata, RowState);
    }

    /// <summary>
    /// Builds the service over the tables, keeping the chosen orderings and
    /// hidden flags somewhere else than <see cref="RowState"/>.
    /// </summary>
    /// <param name="metadata">Finds the series and episodes, called on first use.</param>
    /// <param name="rowState">Keeps the chosen orderings and hidden flags.</param>
    /// <returns>The service.</returns>
    public MetadataOrderingService Build(Func<IMetadataService> metadata, IOrderingRowState rowState)
        => new(
            Orderings,
            Groups,
            Entries,
            rowState,
            ShokoEpisodes.Object,
            TextStore,
            new Lazy<IMetadataService>(metadata),
            new Lazy<AnimeSeriesService>(() => throw new InvalidOperationException("The tests do not update Shoko stats.")),
            new Lazy<AnimeGroupService>(() => throw new InvalidOperationException("The tests do not update Shoko stats.")),
            new Lazy<MetadataEntityCleanup>(() => new(
                Mock.Of<IMetadataPeopleStore>(),
                Mock.Of<IMetadataTagStore>(),
                Mock.Of<IMetadataStudioStore>(),
                Mock.Of<IMetadataRelationStore>(),
                Mock.Of<IMetadataSuggestionStore>(),
                Images.Object
            )),
            StudioStore,
            NullLogger<MetadataOrderingService>.Instance
        );
}

/// <summary>
/// The chosen orderings and hidden flags kept on the rows, in memory. A
/// series or episode has a row while it exists and is not in
/// <see cref="NotStored"/>, and the state of one without a row reads as
/// unset, as it would once the row went.
/// </summary>
public sealed class FakeOrderingRowState : IOrderingRowState
{
    /// <summary>
    /// The chosen ordering of each series, by series.
    /// </summary>
    public Dictionary<MetadataGuid, MetadataGuid> Preferred { get; } = [];

    /// <summary>
    /// The hidden episodes.
    /// </summary>
    public HashSet<MetadataGuid> Hidden { get; } = [];

    /// <summary>
    /// The series and episodes that exist but have no row, as those only a
    /// plugin's resolver serves.
    /// </summary>
    public HashSet<MetadataGuid> NotStored { get; } = [];

    /// <summary>
    /// Whether a series exists.
    /// </summary>
    public Func<MetadataGuid, bool> SeriesExists { get; set; } = _ => true;

    /// <summary>
    /// Whether an episode exists.
    /// </summary>
    public Func<MetadataGuid, bool> EpisodeExists { get; set; } = _ => true;

    public bool HasSeries(MetadataGuid seriesID)
        => seriesID.EntityType == MetadataEntityType.Series && !NotStored.Contains(seriesID) && SeriesExists(seriesID);

    public MetadataGuid? GetPreferredOrdering(MetadataGuid seriesID)
        => HasSeries(seriesID) ? Preferred.GetValueOrDefault(seriesID) : null;

    public bool SetPreferredOrdering(MetadataGuid seriesID, MetadataGuid? orderingID)
    {
        if (!HasSeries(seriesID) || GetPreferredOrdering(seriesID) == orderingID)
            return false;

        if (orderingID is null)
            Preferred.Remove(seriesID);
        else
            Preferred[seriesID] = orderingID;
        return true;
    }

    public bool HasEpisode(MetadataGuid episodeID)
        => episodeID.EntityType == MetadataEntityType.Episode && episodeID.Source != MetadataSource.Shoko && !NotStored.Contains(episodeID) && EpisodeExists(episodeID);

    public bool IsHidden(MetadataGuid episodeID)
        => HasEpisode(episodeID) && Hidden.Contains(episodeID);

    public bool SetHidden(MetadataGuid episodeID, bool hidden)
        => HasEpisode(episodeID) && (hidden ? Hidden.Add(episodeID) : Hidden.Remove(episodeID));
}
