using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Actions;
using Shoko.Server.Filters;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.Shoko.Embedded;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Models;

/// <summary>
/// Covers how a user with restricted tags sees groups: a group is visible when it holds at least
/// one series the user may see, and shows only those series, named after them.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class GroupVisibilityTests
{
    private const string HiddenTag = "secret";

    #region World

    /// <summary>
    /// A mixed group, whose main series the user may not see, holding a visible series directly
    /// and a hidden one in a sub-group, and a group holding only a hidden series.
    /// </summary>
    private sealed class World : IDisposable
    {
        private readonly RepoFactoryScope _scope;

        public AnimeSeries VisibleSeries { get; } = new() { AnimeSeriesID = 10, AniDB_ID = 1, AnimeGroupID = 100 };

        public AnimeSeries HiddenMainSeries { get; } = new() { AnimeSeriesID = 20, AniDB_ID = 2, AnimeGroupID = 100 };

        public AnimeSeries HiddenSubSeries { get; } = new() { AnimeSeriesID = 30, AniDB_ID = 3, AnimeGroupID = 101 };

        public AnimeSeries HiddenOnlySeries { get; } = new() { AnimeSeriesID = 40, AniDB_ID = 4, AnimeGroupID = 200 };

        public AnimeGroup MixedGroup { get; } = new() { AnimeGroupID = 100, DefaultAnimeSeriesID = 20 };

        public AnimeGroup HiddenSubGroup { get; } = new() { AnimeGroupID = 101, AnimeGroupParentID = 100 };

        public AnimeGroup HiddenGroup { get; } = new() { AnimeGroupID = 200 };

        public JMMUser User { get; } = new() { JMMUserID = 1, HideCategories = HiddenTag };

        public AnimeGroupRepository Groups { get; }

        public AnimeSeriesRepository Series { get; }

        public MetadataTextManager Manager { get; }

        public ImageManager Images { get; }

        public static readonly Guid UploadImage = Guid.Parse("0a000000-0000-0000-0000-000000000000");

        public static readonly Guid HiddenProviderImage = Guid.Parse("0b000000-0000-0000-0000-000000000000");

        public static readonly Guid HiddenUploadImage = Guid.Parse("0c000000-0000-0000-0000-000000000000");

        public World()
        {
            var store = new MetadataTextStore(new TextCache(), new CacheOnlyRowWriter());
            var service = new Mock<IMetadataService>();
            service.Setup(s => s.GetSeriesCrossReferences(It.IsAny<int>(), It.IsAny<MetadataSource?>())).Returns([]);
            service.Setup(s => s.GetEpisodeCrossReferences(It.IsAny<int>(), It.IsAny<MetadataSource?>())).Returns([]);
            service.Setup(s => s.GetMovieCrossReferences(It.IsAny<int>(), It.IsAny<MetadataSource?>())).Returns([]);
            service.Setup(s => s.GetMovieCrossReferencesForSeries(It.IsAny<int>(), It.IsAny<MetadataSource?>())).Returns([]);
            service.Setup(s => s.GetSeasonCrossReferences(It.IsAny<int>(), It.IsAny<MetadataSource?>())).Returns([]);
            service.Setup(s => s.GetEpisodeCrossReferencesForSeries(It.IsAny<int>(), It.IsAny<MetadataSource?>())).Returns([]);
            service.Setup(s => s.GetCollectionsWith(It.IsAny<MetadataGuid>())).Returns([]);
            var manager = Manager = TestTextManager.Build(store, service.Object);

            // An upload to the group, and two images of the hidden main series, one fetched and one
            // uploaded to it, which the group was given as preferred ones.
            var imageRows = new[]
            {
                new ShokoImage { ID = UploadImage, PrimaryID = UploadImage, Source = MetadataSource.User, ResourceID = "upload", IsAvailable = true },
                new ShokoImage { ID = HiddenProviderImage, PrimaryID = HiddenProviderImage, Source = MetadataSource.TMDB, ResourceID = "/hidden.jpg", IsAvailable = true },
                new ShokoImage { ID = HiddenUploadImage, PrimaryID = HiddenUploadImage, Source = MetadataSource.User, ResourceID = "hidden", IsAvailable = true },
            };
            var xrefRows = new[]
            {
                Link(1, imageRows[0], ((IMetadata)MixedGroup).ID, ImageEntityType.Primary, preferred: true),
                Link(2, imageRows[1], ((IMetadata)HiddenMainSeries).ID, ImageEntityType.Backdrop, preferred: false),
                Link(3, imageRows[1], ((IMetadata)MixedGroup).ID, ImageEntityType.Backdrop, preferred: true),
                Link(4, imageRows[2], ((IMetadata)HiddenMainSeries).ID, ImageEntityType.Banner, preferred: false),
                Link(5, imageRows[2], ((IMetadata)MixedGroup).ID, ImageEntityType.Banner, preferred: true),
            };
            var imageRepository = CachedRepo.Build<ShokoImageRepository, Guid, ShokoImage>(image => image.ID, imageRows);
            var xrefRepository = CachedRepo.Build<ShokoImage_EntityRepository, int, ShokoImage_Entity>(xref => xref.ID, xrefRows);
            var constructor = typeof(ImageManager).GetConstructors().Single();
            Images = (ImageManager)constructor.Invoke(constructor.GetParameters()
                .Select(parameter => parameter.ParameterType switch
                {
                    var type when type == typeof(ILogger<ImageManager>) => NullLogger<ImageManager>.Instance,
                    var type when type == typeof(ShokoImage_EntityRepository) => xrefRepository,
                    var type when type == typeof(ShokoImageRepository) => imageRepository,
                    _ => (object?)null,
                })
                .ToArray());

            Groups = CachedRepo.Build<AnimeGroupRepository, int, AnimeGroup>(group => group.AnimeGroupID, [MixedGroup, HiddenSubGroup, HiddenGroup]);
            Series = CachedRepo.Build<AnimeSeriesRepository, int, AnimeSeries>(
                series => series.AnimeSeriesID,
                [VisibleSeries, HiddenMainSeries, HiddenSubSeries, HiddenOnlySeries]
            );
            _scope = new RepoFactoryScope()
                .Set(manager)
                .Set(Groups)
                .Set(Series)
                .Set(imageRepository)
                .Set(xrefRepository)
                .With<AniDB_EpisodeRepository, int, AniDB_Episode>(episode => episode.AniDB_EpisodeID, [])
                .With<AniDB_AnimeRepository, int, AniDB_Anime>(anime => anime.AniDB_AnimeID, [
                    Anime(1, "Visible Show", "Seen."),
                    Anime(2, "Hidden Show", "Unseen.", HiddenTag),
                    Anime(3, "Hidden Sequel", "Unseen too.", HiddenTag),
                    Anime(4, "Hidden Alone", "Unseen alone.", HiddenTag),
                ]);

            foreach (var (id, title) in new[] { (1, "Visible Show"), (2, "Hidden Show"), (3, "Hidden Sequel"), (4, "Hidden Alone") })
                store.SetTitles(new(MetadataSource.AniDB, MetadataEntityType.Series, id.ToString()), MetadataSource.AniDB, [
                    new TitleStub { Source = MetadataSource.AniDB, Value = title, Language = TitleLanguage.Romaji, LanguageCode = "x-jat", Type = TitleType.Main },
                ]);
        }

        private static ShokoImage_Entity Link(int id, ShokoImage image, MetadataGuid entityID, ImageEntityType type, bool preferred)
        {
            var entity = new Mock<IWithImages>();
            entity.SetupGet(e => e.ID).Returns(entityID);
            var data = new ImageCrossReferenceData
            {
                ImageType = type,
                Source = MetadataSource.User,
                IsEnabled = true,
                IsDesired = true,
                IsPreferred = preferred,
                Ordering = id,
            };
            return new(image, entity.Object, data, 0) { ID = id };
        }

        private static AniDB_Anime Anime(int id, string title, string description, string tags = "")
            => new() { AniDB_AnimeID = id, AnimeID = id, MainTitle = title, Description = description, AllTags = tags };

        public void Dispose()
            => _scope.Dispose();
    }

    #endregion

    [Fact]
    public void AMixedGroupIsVisibleAndListsOnlyTheVisibleSeries()
    {
        using var world = new World();

        Assert.True(world.User.AllowedGroup(world.MixedGroup));
        Assert.True(world.User.IsAllowedToSee(world.MixedGroup));
        Assert.False(world.User.AllowedWholeGroup(world.MixedGroup));

        var view = AnimeGroupView.For(world.MixedGroup, world.User);
        Assert.True(view.IsVisible);
        Assert.False(view.IsComplete);
        Assert.Equal([10], view.AllSeries.Select(series => series.AnimeSeriesID));
        Assert.Equal([10], view.Series.Select(series => series.AnimeSeriesID));
        Assert.Empty(view.Children);
        Assert.Null(view.PreferredSeriesID);
    }

    [Fact]
    public void AGroupOfOnlyHiddenSeriesStaysHidden()
    {
        using var world = new World();

        Assert.False(world.User.AllowedGroup(world.HiddenGroup));
        Assert.False(world.User.IsAllowedToSee(world.HiddenGroup));
        Assert.False(world.User.AllowedGroup(world.HiddenSubGroup));
        Assert.False(AnimeGroupView.For(world.HiddenGroup, world.User).IsVisible);
    }

    [Fact]
    public void TheNameFallsBackWhenTheMainSeriesIsHidden()
    {
        using var world = new World();

        var view = AnimeGroupView.For(world.MixedGroup, world.User);
        Assert.Equal("Hidden Show", world.MixedGroup.GroupName);
        Assert.Equal(20, view.Group.MainSeries?.AnimeSeriesID);
        Assert.Equal(10, view.MainSeries?.AnimeSeriesID);
        Assert.Equal("Visible Show", view.Name);
        Assert.Equal("Seen.", view.Description);
        Assert.Same(world.VisibleSeries, view.ImageEntity);

        var unrestricted = AnimeGroupView.For(world.MixedGroup, new JMMUser { JMMUserID = 2 });
        Assert.True(unrestricted.IsComplete);
        Assert.Equal("Hidden Show", unrestricted.Name);
    }

    [Fact]
    public void AGroupLevelFilterIncludesTheMixedGroupWithOnlyItsVisibleSeries()
    {
        using var world = new World();
        var engine = new FilteringEngine(NullLogger<FilteringEngine>.Instance, world.Groups, world.Series);
        var filter = new FilterPreset { FilterPresetID = 1, ApplyAtSeriesLevel = false };

        var results = engine.EvaluateFilterWithTuples(filter, world.User, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal([(100, 10)], results);

        var batched = engine.BatchPrepareFiltersWithTuples([filter], world.User, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal([(100, 10)], batched[filter]);

        // The filters read the group as the user sees it.
        var filterable = new FilterableAnimeGroup(world.MixedGroup, DateTime.Now, AnimeGroupView.For(world.MixedGroup, world.User));
        Assert.Equal("Visible Show", filterable.Name);
        Assert.Equal(1, filterable.SeriesCount);
        Assert.Equal(0, filterable.GroupCount);
        Assert.DoesNotContain("Hidden Show", filterable.Names);
    }

    [Fact]
    public void ACustomNamedMixedGroupShowsItsOwnUploadsOnly()
    {
        using var world = new World();
        Assert.True(world.Manager.SetCustomTitle(((IMetadata)world.MixedGroup).ID, "My Group"));

        var view = AnimeGroupView.For(world.MixedGroup, world.User);
        Assert.Equal("My Group", view.Name);
        Assert.True(view.ShowsGroupUploads);

        // The upload stays, preferred; the hidden series' images the group was given do not.
        var uploads = view.GetGroupUploadCrossReferences(world.Images);
        var upload = Assert.Single(uploads);
        Assert.Equal((World.UploadImage, ImageEntityType.Primary, true), (upload.ImageID, upload.ImageType, upload.IsPreferred));
        Assert.Empty(view.GetGroupUploadCrossReferences(world.Images, new() { IsPreferred = true, ImageType = ImageEntityType.Backdrop }));
        Assert.Empty(view.GetGroupUploadCrossReferences(world.Images, new() { IsPreferred = true, ImageType = ImageEntityType.Banner }));
    }

    [Fact]
    public void AMixedGroupWithoutACustomNameShowsNoGroupUploads()
    {
        using var world = new World();

        var view = AnimeGroupView.For(world.MixedGroup, world.User);
        Assert.False(view.ShowsGroupUploads);
        Assert.Empty(view.GetGroupUploadCrossReferences(world.Images));
    }

    #region Actions and playlists

    private sealed class VisibleSeriesAction : GroupAction, IVisibleSeriesGroupAction
    {
        public override string Name => "Visible";

        public override ActionPermission Permission => ActionPermission.Admin;

        public override Task Execute(CancellationToken token = default) => Task.CompletedTask;
    }

    private sealed class WholeGroupAction : GroupAction
    {
        public override string Name => "Whole";

        public override ActionPermission Permission => ActionPermission.Admin;

        public override Task Execute(CancellationToken token = default) => Task.CompletedTask;
    }

    [Fact]
    public void AGroupActionNeedsTheWholeGroupUnlessItOnlyTouchesVisibleSeries()
    {
        using var world = new World();

        Assert.Null(ActionService.CheckVisible(new VisibleSeriesAction(), world.MixedGroup, world.User));
        Assert.NotNull(ActionService.CheckVisible(new WholeGroupAction(), world.MixedGroup, world.User));
        Assert.NotNull(ActionService.CheckVisible(new VisibleSeriesAction(), world.HiddenGroup, world.User));
        Assert.Null(ActionService.CheckVisible(new WholeGroupAction(), world.MixedGroup, null));
    }

    [Fact]
    public void AnActionOnAHiddenSeriesOrItsEpisodeIsRefused()
    {
        using var world = new World();
        var action = new WholeGroupAction();

        Assert.Null(ActionService.CheckVisible(action, world.VisibleSeries, world.User));
        Assert.NotNull(ActionService.CheckVisible(action, world.HiddenMainSeries, world.User));
        Assert.NotNull(ActionService.CheckVisible(action, new AnimeEpisode { AnimeEpisodeID = 1, AnimeSeriesID = 20 }, world.User));
    }

    [Theory]
    [InlineData("g200", "Unknown group ID \"g200\".")]
    [InlineData("s40", "Unknown series ID \"s40\".")]
    [InlineData("a4", "Unknown series ID \"a4\".")]
    public void APlaylistReportsWhatTheUserMayNotSeeAsUnknown(string item, string error)
    {
        using var world = new World();
        var service = new GeneratedPlaylistService(
            systemService: null!, imageManager: null!, contextAccessor: null!,
            groupRepository: world.Groups, animeSeriesService: null!, seriesRepository: world.Series,
            episodeRepository: CachedRepo.Build<AnimeEpisodeRepository, int, AnimeEpisode>(e => e.AnimeEpisodeID, []),
            videoRepository: CachedRepo.Build<VideoLocalRepository, int, VideoLocal>(v => v.VideoLocalID, []),
            userService: null!);
        var state = new ModelStateDictionary();

        Assert.False(service.TryParsePlaylist([item], out _, state, user: world.User));
        Assert.Equal(error, Assert.Single(state.Values.SelectMany(value => value.Errors)).ErrorMessage);
    }

    #endregion
}
