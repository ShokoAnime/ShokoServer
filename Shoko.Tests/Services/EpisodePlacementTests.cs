using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Metadata.Embedded;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Services;
using Shoko.Tests.API.Metadata;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers where each source places a Shoko special: AniDB by its titles, a
/// linked plugin source by its default ordering, each mapped back to the
/// Shoko episodes of the special's series.
/// </summary>
public class EpisodePlacementTests
{
    #region Helpers

    /// <summary>
    /// Shoko series 5 over AniDB anime 9: regulars 101 to 103 (Shoko 1001 to
    /// 1003), special 201 titled "Episode 1.5" (Shoko 1004) and special 202
    /// (Shoko 1005). AniDB episode 301 is Shoko 2001, of series 6. The plugin
    /// series <c>p</c> has <c>p-e1</c> to <c>p-e3</c>, <c>p-sp</c> airing
    /// before <c>p-e3</c> and <c>p-sp2</c> airing before <c>p-e1</c>.
    /// </summary>
    private sealed class World
    {
        private readonly Dictionary<MetadataGuid, IMetadata> _entries = [];

        private readonly List<IMetadataEpisodeCrossReference> _links = [];

        public OrderingTables Tables { get; }

        public MetadataOrderingService Service { get; }

        public World()
        {
            Tables = new(
                Shoko(1001, 101, 5),
                Shoko(1002, 102, 5),
                Shoko(1003, 103, 5),
                Shoko(1004, 201, 5),
                Shoko(1005, 202, 5),
                Shoko(2001, 301, 6)
            );
            var metadata = new Mock<IMetadataService>();
            metadata.Setup(service => service.GetSeries(It.IsAny<MetadataGuid>())).Returns((MetadataGuid id) => _entries.GetValueOrDefault(id) as ISeries);
            metadata.Setup(service => service.GetEpisode(It.IsAny<MetadataGuid>())).Returns((MetadataGuid id) => _entries.GetValueOrDefault(id) as IEpisode);
            Tables.CrossReferences.Setup(store => store.GetEpisodeLinks(It.IsAny<int>(), It.IsAny<MetadataSource?>()))
                .Returns((int anidbEpisodeID, MetadataSource? source) => [.. _links.Where(link => link.AnidbEpisodeID == anidbEpisodeID && (source is null || link.Source == source))]);
            Tables.CrossReferences.Setup(store => store.GetLinksTo(It.IsAny<MetadataGuid>()))
                .Returns((MetadataGuid entry) => [.. _links.Where(link => link.ProviderID == entry)]);
            Service = Tables.Build(() => metadata.Object);

            AddAnime();
            AddPluginSeries();
            Link(102, "p-e2");
            Link(103, "p-e3");
            Link(201, "p-sp");
            Link(202, "p-sp2");
            Link(301, "p-e1");
        }

        private void AddAnime()
        {
            var seriesID = new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, "9");
            var series = new Mock<ISeries>();
            ISeason[] seasons = [Season(seriesID, 1), Season(seriesID, 0)];
            IEpisode[] episodes =
            [
                Anidb(series, 101, 1, 1),
                Anidb(series, 102, 1, 2),
                Anidb(series, 103, 1, 3),
                Anidb(series, 201, 0, 1, "Episode 1.5"),
                Anidb(series, 202, 0, 2, "Recap"),
            ];
            Finish(series, seriesID, seasons, episodes);
        }

        private void AddPluginSeries()
        {
            Metadata_Episode[] episodes =
            [
                Stored("p-e1", 1, 1),
                Stored("p-e2", 1, 2),
                Stored("p-e3", 1, 3),
                Stored("p-sp", 0, 1, new() { AirsBeforeSeasonNumber = 1, AirsBeforeEpisodeNumber = 3 }),
                Stored("p-sp2", 0, 2, new() { AirsBeforeSeasonNumber = 1, AirsBeforeEpisodeNumber = 1 }),
            ];
            var seriesID = new MetadataGuid(TestSources.Plugin, MetadataEntityType.Series, "p");
            foreach (var episode in episodes)
            {
                episode.Source = TestSources.Plugin;
                episode.SeriesID = "p";
                episode.SeasonID = $"p-{episode.SeasonNumber}";
            }

            Finish(new Mock<ISeries>(), seriesID, [Season(seriesID, 1), Season(seriesID, 0)], [.. episodes]);
        }

        private void Finish(Mock<ISeries> series, MetadataGuid seriesID, IReadOnlyList<ISeason> seasons, IReadOnlyList<IEpisode> episodes)
        {
            series.SetupGet(value => value.ID).Returns(seriesID);
            series.SetupGet(value => value.Seasons).Returns(seasons);
            series.SetupGet(value => value.Episodes).Returns(episodes);
            _entries[seriesID] = series.Object;
            foreach (var episode in episodes)
                _entries[episode.ID] = episode;
        }

        private void Link(int anidbEpisodeID, string pluginEpisodeID)
        {
            var link = new Mock<IMetadataEpisodeCrossReference>();
            link.SetupGet(value => value.AnidbEpisodeID).Returns(anidbEpisodeID);
            link.SetupGet(value => value.Source).Returns(TestSources.Plugin);
            link.SetupGet(value => value.ProviderID).Returns(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Episode, pluginEpisodeID));
            link.SetupGet(value => value.ProviderParentID).Returns(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Series, "p"));
            _links.Add(link.Object);
        }

        private static AnimeEpisode Shoko(int id, int anidbEpisodeID, int seriesID)
            => new() { AnimeEpisodeID = id, AniDB_EpisodeID = anidbEpisodeID, AnimeSeriesID = seriesID };

        private static ISeason Season(MetadataGuid seriesID, int number)
        {
            var season = new Mock<ISeason>();
            season.SetupGet(value => value.ID).Returns(new MetadataGuid(seriesID.Source, MetadataEntityType.Season, $"{seriesID.ID}-{number}"));
            season.SetupGet(value => value.SeriesID).Returns(seriesID);
            season.SetupGet(value => value.SeasonNumber).Returns(number);
            season.SetupGet(value => value.IsSpecial).Returns(number is 0);
            return season.Object;
        }

        private static IEpisode Anidb(Mock<ISeries> series, int id, int season, int number, string? title = null)
        {
            var episode = new Mock<IEpisode>();
            episode.SetupGet(value => value.ID).Returns(new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Episode, id.ToString()));
            episode.SetupGet(value => value.SeriesID).Returns(new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, "9"));
            episode.SetupGet(value => value.SeasonID).Returns(new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Season, $"9-{season}"));
            episode.SetupGet(value => value.SeasonNumber).Returns(season);
            episode.SetupGet(value => value.EpisodeNumber).Returns(number);
            episode.SetupGet(value => value.Type).Returns(season is 0 ? EpisodeType.Special : EpisodeType.Episode);
            episode.SetupGet(value => value.Titles).Returns(title is null ? [] : [new FakeMetadataEntries.FakeTitle(title, source: MetadataSource.AniDB)]);
            episode.SetupGet(value => value.Series).Returns(() => series.Object);
            return episode.Object;
        }

        private static Metadata_Episode Stored(string id, int season, int number, Metadata_EpisodeExtra? extra = null)
            => new()
            {
                ProviderID = id,
                SeasonNumber = season,
                EpisodeNumber = number,
                Type = season is 0 ? EpisodeType.Special : EpisodeType.Episode,
                ExtraData = extra,
            };
    }

    /// <summary>
    /// A Shoko episode of series 5.
    /// </summary>
    private static IShokoEpisode Special(int id, int anidbEpisodeID, EpisodeType type = EpisodeType.Special)
    {
        var episode = new Mock<IShokoEpisode>();
        episode.SetupGet(value => value.ID).Returns(ShokoID(id));
        episode.SetupGet(value => value.ShokoSeriesID).Returns(5);
        episode.SetupGet(value => value.AnidbEpisodeID).Returns(anidbEpisodeID);
        episode.SetupGet(value => value.Type).Returns(type);
        return episode.Object;
    }

    private static MetadataGuid ShokoID(int id)
        => new(MetadataSource.Shoko, MetadataEntityType.Episode, id.ToString());

    private static (string, string?, string?)[] Read(IEnumerable<IEpisodePlacement> placements)
        => [.. placements.Select(placement => (placement.Source.Value, placement.AirsAfterEpisodeID?.ID, placement.AirsBeforeEpisodeID?.ID))];

    #endregion

    #region Placements

    [Fact]
    public void ASpecialIsPlacedByItsAnidbTitleAndByItsLinkedEpisodeMappedBackToTheSeries()
    {
        var world = new World();

        var placements = world.Service.GetEpisodePlacements(Special(1004, 201));

        Assert.Equal([("anidb", "1001", "1002"), (TestSources.Plugin.Value, "1002", "1003")], Read(placements));
        Assert.All(placements, placement => Assert.Equal(ShokoID(1004), placement.EpisodeID));
        Assert.Equal(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Episode, "p-sp"), placements[1].SourceEpisodeID);
    }

    [Fact]
    public void ASourceWhoseNeighbourIsOutsideTheSeriesIsSkipped()
    {
        var world = new World();

        Assert.Empty(world.Service.GetEpisodePlacements(Special(1005, 202)));
    }

    [Fact]
    public void TheSourceFilterKeepsOneSource()
    {
        var world = new World();

        Assert.Equal([("anidb", "1001", "1002")], Read(world.Service.GetEpisodePlacements(Special(1004, 201), MetadataSource.AniDB)));
        Assert.Equal([(TestSources.Plugin.Value, "1002", "1003")], Read(world.Service.GetEpisodePlacements(Special(1004, 201), TestSources.Plugin)));
    }

    [Fact]
    public void ARegularEpisodeHasNoPlacements()
    {
        var world = new World();

        Assert.Empty(world.Service.GetEpisodePlacements(Special(1002, 102, EpisodeType.Episode)));
        world.Tables.CrossReferences.VerifyNoOtherCalls();
    }

    #endregion

    #region APIv3

    [Fact]
    public void WithoutTheIncludeNothingIsWorkedOutAndTheFieldIsLeftOut()
    {
        var services = new Mock<IServiceProvider>(MockBehavior.Strict);
        var context = new DefaultHttpContext { RequestServices = services.Object };

        Assert.Null(Episode.PlacementResolver(context, null));
        Assert.Null(Episode.PlacementResolver(context, new HashSet<Episode.IncludeDetails>()));
        services.VerifyNoOtherCalls();

        var json = JObject.Parse(JsonConvert.SerializeObject(RuntimeHelpers.GetUninitializedObject(typeof(Episode))));
        Assert.False(json.ContainsKey(nameof(Episode.Placements)));
    }

    #endregion
}
