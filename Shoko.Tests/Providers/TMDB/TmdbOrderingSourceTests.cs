using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories.Cached.TMDB;
using Shoko.Server.Repositories.Direct.TMDB.Optional;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Providers.TMDB;

/// <summary>
/// Covers <see cref="TmdbOrderingSource"/> through the ordering service: a
/// TMDB show's default ordering keeps its ID and its TMDB types, TMDB's
/// episode groups are read as orderings that can be chosen, and a TMDB
/// episode's hidden state is kept by the core.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class TmdbOrderingSourceTests
{
    #region Helpers

    private const string CollectionID = "5f0c1a2b3c4d5e6f7a8b9c0d";

    private const string GroupID = "5f0c1a2b3c4d5e6f7a8b9c0e";

    private static MetadataGuid ID(MetadataEntityType entityType, string id)
        => new(MetadataSource.TMDB, entityType, id);

    /// <summary>
    /// A TMDB show with two seasons of two episodes each, and one episode
    /// group that puts the last episode first.
    /// </summary>
    private sealed class World : IDisposable
    {
        private readonly RepoFactoryScope _scope;

        public TMDB_Show Show { get; } = new(5) { EnglishTitle = "Show" };

        public IReadOnlyList<TMDB_Episode> Episodes { get; }

        public TMDB_AlternateOrdering Group { get; } = new(CollectionID)
        {
            TMDB_AlternateOrderingID = 1,
            TmdbShowID = 5,
            EnglishTitle = "DVD Order",
            EnglishOverview = "As on the discs.",
            Type = OrderingType.DVD,
        };

        public IReadOnlyList<TMDB_AlternateOrdering_Episode> GroupEpisodes { get; }

        public MetadataOrderingService Service { get; }

        public Mock<TMDB_AlternateOrderingRepository> Orderings { get; } = new((object)null!);

        public World()
        {
            var seasons = new[]
            {
                new TMDB_Season(51) { TMDB_SeasonID = 1, TmdbShowID = 5, SeasonNumber = 1 },
                new TMDB_Season(52) { TMDB_SeasonID = 2, TmdbShowID = 5, SeasonNumber = 2 },
            };
            Episodes =
            [
                new TMDB_Episode(501) { TMDB_EpisodeID = 1, TmdbShowID = 5, TmdbSeasonID = 51, SeasonNumber = 1, EpisodeNumber = 1 },
                new TMDB_Episode(502) { TMDB_EpisodeID = 2, TmdbShowID = 5, TmdbSeasonID = 51, SeasonNumber = 1, EpisodeNumber = 2 },
                new TMDB_Episode(503) { TMDB_EpisodeID = 3, TmdbShowID = 5, TmdbSeasonID = 52, SeasonNumber = 2, EpisodeNumber = 1 },
                new TMDB_Episode(504) { TMDB_EpisodeID = 4, TmdbShowID = 5, TmdbSeasonID = 52, SeasonNumber = 2, EpisodeNumber = 2 },
            ];
            GroupEpisodes =
            [
                .. new[] { 504, 501, 502, 503 }.Select((episodeID, index) => new TMDB_AlternateOrdering_Episode(GroupID, episodeID)
                {
                    TmdbShowID = 5,
                    TmdbEpisodeGroupCollectionID = CollectionID,
                    SeasonNumber = 1,
                    EpisodeNumber = index + 1,
                }),
            ];

            var orderings = Orderings;
            orderings.Setup(repository => repository.GetByTmdbShowID(5)).Returns([Group]);
            orderings.Setup(repository => repository.GetByTmdbEpisodeGroupCollectionID(It.IsAny<string>()))
                .Returns((string id) => id == CollectionID ? Group : null);
            var episodes = new Mock<TMDB_AlternateOrdering_EpisodeRepository>((object)null!);
            episodes.Setup(repository => repository.GetByTmdbEpisodeGroupCollectionID(CollectionID)).Returns(GroupEpisodes);
            episodes.Setup(repository => repository.GetByTmdbEpisodeGroupID(GroupID)).Returns(GroupEpisodes);
            episodes.Setup(repository => repository.GetByTmdbEpisodeID(It.IsAny<int>()))
                .Returns((int id) => [.. GroupEpisodes.Where(episode => episode.TmdbEpisodeID == id)]);
            var groupSeasons = new Mock<TMDB_AlternateOrdering_SeasonRepository>((object)null!);
            groupSeasons.Setup(repository => repository.GetByTmdbEpisodeGroupCollectionID(CollectionID)).Returns([]);

            _scope = new RepoFactoryScope()
                .With<TMDB_ShowRepository, int, TMDB_Show>(show => show.TmdbShowID, [Show])
                .With<TMDB_SeasonRepository, int, TMDB_Season>(season => season.TMDB_SeasonID, seasons)
                .With<TMDB_EpisodeRepository, int, TMDB_Episode>(episode => episode.TMDB_EpisodeID, Episodes)
                .Set(orderings.Object)
                .Set(episodes.Object)
                .Set(groupSeasons.Object);

            var metadata = new Mock<IMetadataService>();
            metadata.Setup(service => service.GetSeries(It.IsAny<MetadataGuid>()))
                .Returns((MetadataGuid id) => id == ID(MetadataEntityType.Series, "5") ? Show : null);
            metadata.Setup(service => service.GetEpisode(It.IsAny<MetadataGuid>()))
                .Returns((MetadataGuid id) => Episodes.FirstOrDefault(episode => ((IMetadata)episode).ID == id));
            Service = new OrderingTables().Build(() => metadata.Object, new TmdbOrderingSource(orderings.Object, episodes.Object));
        }

        public void Dispose()
            => _scope.Dispose();
    }

    #endregion

    #region Orderings

    [Fact]
    public void AShowsDefaultOrderingKeepsItsIDAndItsTmdbTypes()
    {
        using var world = new World();

        var orderings = world.Service.GetOrderings(world.Show);

        Assert.Equal([ID(MetadataEntityType.Ordering, "5"), ID(MetadataEntityType.Ordering, CollectionID)], orderings.Select(ordering => ordering.ID));
        var ordering = Assert.IsType<TMDB_Show_DefaultOrdering>(orderings[0]);
        Assert.True(ordering.IsDefault);
        Assert.True(ordering.IsPreferred);
        Assert.Equal(OrderingType.Default, ordering.Type);
        Assert.Equal(5, ordering.TmdbShowID);
        Assert.Same(world.Show, ((ITmdbShowOrderingInformation)ordering).Series);
        Assert.Equal(["51", "52"], ((ITmdbShowOrderingInformation)ordering).Seasons.Select(season => season.ID.ID));
        Assert.Equal([501, 502, 503, 504], ((ITmdbShowOrderingInformation)ordering).Episodes.Select(episode => episode.TmdbID));
        Assert.IsType<TMDB_Show_DefaultOrdering>(world.Service.GetOrdering(ID(MetadataEntityType.Ordering, "5")));
    }

    [Fact]
    public void AnEpisodeGroupIsReadAsAnOrderingOfTheShow()
    {
        using var world = new World();

        var ordering = Assert.IsAssignableFrom<ITmdbShowOrderingInformation>(world.Service.GetOrdering(ID(MetadataEntityType.Ordering, CollectionID)));

        Assert.Same(world.Group, ordering);
        Assert.Equal(ID(MetadataEntityType.Series, "5"), ordering.SeriesID);
        Assert.False(ordering.IsDefault);
        Assert.Equal(4, ordering.EpisodeCount);
        Assert.Equal([504, 501, 502, 503], ordering.Episodes.Select(episode => episode.TmdbID));
    }

    [Fact]
    public void AnEpisodeGroupCanBeChosenAndTheShowFallsBackWhenTmdbDropsIt()
    {
        using var world = new World();
        var groupID = ID(MetadataEntityType.Ordering, CollectionID);

        Assert.True(world.Service.SetPreferredOrdering(ID(MetadataEntityType.Series, "5"), groupID));
        Assert.Same(world.Group, world.Service.GetPreferredOrdering(world.Show));
        Assert.False(world.Service.GetDefaultOrdering(world.Show).IsPreferred);

        world.Orderings.Setup(repository => repository.GetByTmdbEpisodeGroupCollectionID(CollectionID)).Returns((TMDB_AlternateOrdering?)null);
        Assert.IsType<TMDB_Show_DefaultOrdering>(world.Service.GetPreferredOrdering(world.Show));
        Assert.True(world.Service.GetDefaultOrdering(world.Show).IsPreferred);
    }

    #endregion

    #region Episode Orderings

    [Fact]
    public void AnEpisodeHasItsDefaultPlaceAndOneForEachEpisodeGroup()
    {
        using var world = new World();
        var episode = world.Episodes[3];

        var places = world.Service.GetEpisodeOrderings(episode);

        Assert.Equal(2, places.Count);
        var place = Assert.IsType<TMDB_Episode_DefaultOrdering>(places[0]);
        Assert.Equal(ID(MetadataEntityType.Ordering, "5"), place.OrderingID);
        Assert.Equal(ID(MetadataEntityType.Season, "52"), place.SeasonID);
        Assert.Equal(2, place.SeasonNumber);
        Assert.Equal(2, place.EpisodeNumber);
        Assert.Equal(EpisodeType.Episode, place.EpisodeType);
        Assert.Equal(504, place.TmdbEpisodeID);
        Assert.Same(episode, ((ITmdbEpisodeOrderingInformation)place).Episode);

        var groupPlace = Assert.IsAssignableFrom<ITmdbEpisodeOrderingInformation>(places[1]);
        Assert.Equal(ID(MetadataEntityType.Ordering, CollectionID), groupPlace.OrderingID);
        Assert.Equal(ID(MetadataEntityType.Season, GroupID), groupPlace.SeasonID);
        Assert.Equal(ID(MetadataEntityType.Episode, "504"), groupPlace.EpisodeID);
        Assert.Equal(1, groupPlace.SeasonNumber);
        Assert.Equal(1, groupPlace.EpisodeNumber);
        Assert.Equal(EpisodeType.Episode, groupPlace.EpisodeType);
        Assert.False(groupPlace.IsDefault);
    }

    [Fact]
    public void AnEpisodeInAnEpisodeGroupOfOrderZeroIsASpecialThere()
    {
        using var world = new World();
        world.GroupEpisodes[0].SeasonNumber = 0;

        var places = world.Service.GetEpisodeOrderings(world.Episodes[3]);

        // TMDB's own numbers are kept; the episode is a regular one in its own season.
        Assert.Equal(EpisodeType.Episode, places[0].EpisodeType);
        Assert.Equal((0, 1, EpisodeType.Special), (places[1].SeasonNumber, places[1].EpisodeNumber, places[1].EpisodeType));
    }

    #endregion
}
