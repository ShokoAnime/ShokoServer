using System;
using System.Collections.Generic;
using System.Reflection;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers the order a Shoko series' description is looked for in, within one language: every
/// source's best-fitting entry first, then the shows behind later seasons, then AniDB's notes.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class SeriesDescriptionWalkTests
{
    private const int AnimeID = 1;

    private const string Synopsis = "A boy finds a sword.";

    private const string Note = "* Based on a light novel.";

    private const string ShowText = "The whole show.";

    private const string SeasonText = "This season.";

    private static readonly MetadataGuid _show = new(MetadataSource.TMDB, MetadataEntityType.Series, "5");

    #region World

    /// <summary>
    /// One anime and its Shoko series, linked to a TMDB show whose seasons and season links the
    /// test sets, all reading their overviews from one cache-only store.
    /// </summary>
    private sealed class World : IDisposable
    {
        private readonly RepoFactoryScope _scope;

        public MetadataTextStore Store { get; } = new(new TextCache(), new CacheOnlyRowWriter());

        public MetadataTextManager Manager { get; }

        public List<ISeason> ShowSeasons { get; } = [];

        public List<IMetadataSeasonCrossReference> SeasonLinks { get; } = [];

        public AnimeSeries Series { get; } = new() { AnimeSeriesID = 2, AniDB_ID = AnimeID };

        public World(string anidbDescription, params int[] linkedSeasons)
        {
            var service = new Mock<IMetadataService>();
            service.Setup(s => s.GetSeriesCrossReferences(AnimeID, MetadataSource.TMDB)).Returns(() => [ShowLink()]);
            service.Setup(s => s.GetSeasonCrossReferences(AnimeID, MetadataSource.TMDB)).Returns(() => SeasonLinks);
            service.Setup(s => s.GetEpisodeCrossReferencesForSeries(It.IsAny<int>(), It.IsAny<MetadataSource?>())).Returns([]);
            service.Setup(s => s.GetMovieCrossReferencesForSeries(It.IsAny<int>(), It.IsAny<MetadataSource?>())).Returns([]);
            service.Setup(s => s.GetCollectionsWith(It.IsAny<MetadataGuid>())).Returns([]);
            Manager = TestTextManager.Build(Store, service.Object);
            _scope = new RepoFactoryScope()
                .Set(Manager)
                .With<AnimeSeriesRepository, int, AnimeSeries>(series => series.AnimeSeriesID, [Series])
                .With<AniDB_AnimeRepository, int, AniDB_Anime>(
                    anime => anime.AniDB_AnimeID,
                    [new() { AniDB_AnimeID = AnimeID, AnimeID = AnimeID, AnimeType = AnimeType.TV, Description = anidbDescription }]
                );

            foreach (var seasonNumber in linkedSeasons)
            {
                ShowSeasons.Add(Season(seasonNumber));
                SeasonLinks.Add(SeasonLink(seasonNumber));
            }
        }

        public static MetadataGuid SeasonID(int seasonNumber)
            => new(MetadataSource.TMDB, MetadataEntityType.Season, $"5-{seasonNumber}");

        public void Describe(MetadataGuid entry, string value)
            => Store.SetOverviews(entry, MetadataSource.TMDB, [new TextStub { Source = MetadataSource.TMDB, Value = value, Language = TitleLanguage.English, LanguageCode = "en" }]);

        public (string? Value, SeriesDescriptionStep? Step) Chosen()
            => (Series.PreferredOverview?.Value, Manager.SeriesDescriptionStepOf(((IMetadata)Series).ID));

        public ISeason Season(int seasonNumber)
        {
            var season = new Mock<ISeason>();
            season.SetupGet(s => s.ID).Returns(SeasonID(seasonNumber));
            season.SetupGet(s => s.Source).Returns(MetadataSource.TMDB);
            season.SetupGet(s => s.SeasonNumber).Returns(seasonNumber);
            season.SetupGet(s => s.Episodes).Returns([Mock.Of<IEpisode>()]);
            season.SetupGet(s => s.Overviews).Returns(() => Store.GetOverviews(SeasonID(seasonNumber), MetadataSource.TMDB));
            return season.Object;
        }

        private IMetadataSeasonCrossReference SeasonLink(int seasonNumber)
        {
            var link = new Mock<IMetadataSeasonCrossReference>();
            link.SetupGet(l => l.AnidbAnimeID).Returns(AnimeID);
            link.SetupGet(l => l.Source).Returns(MetadataSource.TMDB);
            link.SetupGet(l => l.SeasonNumber).Returns(seasonNumber);
            link.SetupGet(l => l.ProviderID).Returns(SeasonID(seasonNumber));
            link.SetupGet(l => l.Provider).Returns(Season(seasonNumber));
            return link.Object;
        }

        private IMetadataSeriesCrossReference ShowLink()
        {
            var show = new Mock<ISeries>();
            show.SetupGet(s => s.ID).Returns(_show);
            show.SetupGet(s => s.Source).Returns(MetadataSource.TMDB);
            show.SetupGet(s => s.Seasons).Returns(() => ShowSeasons);
            show.SetupGet(s => s.Overviews).Returns(() => Store.GetOverviews(_show, MetadataSource.TMDB));
            var link = new Mock<IMetadataSeriesCrossReference>();
            link.SetupGet(l => l.AnidbAnimeID).Returns(AnimeID);
            link.SetupGet(l => l.Source).Returns(MetadataSource.TMDB);
            link.SetupGet(l => l.ProviderID).Returns(_show);
            link.SetupGet(l => l.Provider).Returns(show.Object);
            return link.Object;
        }

        public void Dispose()
            => _scope.Dispose();
    }

    #endregion

    [Theory]
    [InlineData(Synopsis, false, Synopsis, SeriesDescriptionStep.Entry)]
    [InlineData(Note, false, ShowText, SeriesDescriptionStep.LaterSeasonShow)]
    [InlineData(Note, true, Note, SeriesDescriptionStep.AnidbNote)]
    public void ALaterSeasonWithoutText_IsFollowedByEveryEntryThenItsShowThenAnidbsNotes(string anidb, bool showSilent, string expected, SeriesDescriptionStep step)
    {
        using var world = new World(anidb, 2);
        world.ShowSeasons.Insert(0, world.Season(1));
        if (!showSilent)
            world.Describe(_show, ShowText);

        Assert.Equal((expected, step), world.Chosen());
    }

    [Fact]
    public void AFirstSeasonWithoutText_ReadsItsShowBeforeTheNextSource()
    {
        using var world = new World(Synopsis, 1);
        world.ShowSeasons.Add(world.Season(2));
        world.Describe(_show, ShowText);

        Assert.Equal((ShowText, SeriesDescriptionStep.FirstSeasonShow), world.Chosen());

        world.Describe(World.SeasonID(1), SeasonText);
        Assert.Equal((SeasonText, SeriesDescriptionStep.Entry), world.Chosen());
    }

    [Fact]
    public void ASeasonAddedToTheShow_WorksTheChoiceOutAgain()
    {
        using var world = new World(Synopsis, 1);
        world.Describe(_show, ShowText);
        world.Describe(World.SeasonID(1), SeasonText);
        Assert.Equal(ShowText, world.Series.PreferredOverview?.Value);

        world.ShowSeasons.Add(world.Season(2));
        var seasons = CachedRepo.Build<Metadata_SeasonRepository, int, Metadata_Season>(row => row.Metadata_SeasonID);
        var added = new Metadata_Season { Metadata_SeasonID = 1, Source = MetadataSource.TMDB, ProviderID = "5-2", SeriesID = _show.ID, SeasonNumber = 2 };
        seasons.GetType().GetMethod("UpdateCache", BindingFlags.Instance | BindingFlags.NonPublic, [typeof(Metadata_Season)])!.Invoke(seasons, [added]);

        Assert.Equal(SeasonText, world.Series.PreferredOverview?.Value);
    }
}
