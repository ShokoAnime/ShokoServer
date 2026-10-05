using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Image.Exceptions;
using Shoko.Abstractions.Metadata.Image.Options;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers how <see cref="MetadataImageReconciler"/> turns a provider's image
/// candidates into linked images: which are desired, how they are ordered,
/// what the settings skip, and what is unlinked when the source drops it.
/// </summary>
public class MetadataImageReconcilerTests
{
    #region Fixtures

    private static readonly MetadataSource Source = TestSources.Plugin;

    private sealed class Xref
    {
        public required Guid ImageID { get; init; }

        public required ImageEntityType ImageType { get; init; }

        public required MetadataSource Source { get; init; }

        public int Ordering { get; set; }

        public bool IsDesired { get; set; }

        public double? Rating { get; set; }

        public IImageCrossReference Object { get; set; } = null!;
    }

    /// <summary>
    /// An image manager over plain lists, doing what the reconciler needs.
    /// </summary>
    private sealed class FakeImages
    {
        public Mock<IImageManager> Manager { get; } = new();

        public Dictionary<string, IImage> Images { get; } = [];

        public List<Xref> Xrefs { get; } = [];

        public List<ImageData> Added { get; } = [];

        public int Scheduled { get; private set; }

        public string? Template { get; set; } = "https://example.com/{0}";

        public FakeImages()
        {
            Manager.Setup(m => m.GetTemplateUrlForSource(It.IsAny<MetadataSource>())).Returns(() => Template);
            Manager.Setup(m => m.GetImageBySourceAndRemoteResourceID(It.IsAny<MetadataSource>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns((MetadataSource _, string resourceID, bool _) => Images.GetValueOrDefault(resourceID));
            Manager.Setup(m => m.AddImage(It.IsAny<ImageData>())).Returns((ImageData data) =>
            {
                var image = new Mock<IImage>();
                image.SetupGet(i => i.ID).Returns(IImageManager.GetIDForImageSourceAndResourceID(data.Source, data.ResourceID));
                image.SetupGet(i => i.ResourceID).Returns(data.ResourceID);
                image.SetupGet(i => i.Source).Returns(data.Source);
                Added.Add(data);
                return Images[data.ResourceID] = image.Object;
            });
            Manager.Setup(m => m.GetImageCrossReferencesForEntity(It.IsAny<IWithImages>(), It.IsAny<ImageCrossReferenceFilteringOptions?>()))
                .Returns((IWithImages _, ImageCrossReferenceFilteringOptions? options) => [
                    .. Xrefs
                        .Where(xref => options?.ImageType is not { } type || xref.ImageType == type)
                        .Where(xref => options?.XrefSource is not { } source || xref.Source == source)
                        .Select(xref => xref.Object),
                ]);
            Manager.Setup(m => m.AddImageCrossReference(It.IsAny<IWithImages>(), It.IsAny<IImage>(), It.IsAny<ImageCrossReferenceData>()))
                .Returns((IWithImages _, IImage image, ImageCrossReferenceData data) => Add(new()
                {
                    ImageID = image.ID,
                    ImageType = data.ImageType,
                    Source = data.Source,
                    Ordering = data.Ordering ?? 0,
                    IsDesired = data.IsDesired,
                    Rating = data.Rating,
                }).Object);
            Manager.Setup(m => m.UpdateImageCrossReference(It.IsAny<IImageCrossReference>(), It.IsAny<ImageCrossReferenceUpdateData>()))
                .Returns((IImageCrossReference xref, ImageCrossReferenceUpdateData data) =>
                {
                    var state = Xrefs.Single(each => each.Object == xref);
                    state.Ordering = data.Ordering ?? state.Ordering;
                    state.IsDesired = data.IsDesired ?? state.IsDesired;
                    if (data.HasRatingSet)
                        state.Rating = data.Rating;
                    return xref;
                });
            Manager.Setup(m => m.RemoveImageCrossReference(It.IsAny<IImageCrossReference>()))
                .Returns((IImageCrossReference xref) => Xrefs.RemoveAll(each => each.Object == xref) > 0);
            Manager.Setup(m => m.ScheduleAutoDownloadsForEntity(
                    It.IsAny<IWithImages>(), It.IsAny<MetadataSource?>(), It.IsAny<ImageEntityType?>(), It.IsAny<MetadataSource?>(), It.IsAny<bool>()))
                .Returns(() =>
                {
                    Scheduled++;
                    return Task.CompletedTask;
                });
        }

        public Xref Add(Xref state)
        {
            var xref = new Mock<IImageCrossReference>();
            xref.SetupGet(x => x.ImageID).Returns(state.ImageID);
            xref.SetupGet(x => x.ImageType).Returns(state.ImageType);
            xref.SetupGet(x => x.Source).Returns(state.Source);
            state.Object = xref.Object;
            Xrefs.Add(state);
            return state;
        }

        public Xref Of(string resourceID, ImageEntityType type = ImageEntityType.Primary)
            => Xrefs.Single(xref => xref.ImageID == IImageManager.GetIDForImageSourceAndResourceID(Source, resourceID) && xref.ImageType == type);
    }

    private static IWithImages Entity(MetadataEntityType entityType, string? defaultPoster = null)
    {
        var entity = new Mock<IWithImages>();
        entity.SetupGet(e => e.ID).Returns(new MetadataGuid(Source, entityType, "1"));
        entity.As<IMetadataDefaultImageSource>().Setup(e => e.GetDefaultResourceID(ImageEntityType.Primary)).Returns(defaultPoster);
        return entity.Object;
    }

    private static ImageCandidate Poster(string resourceID, string? language = null, double? rating = null)
        => new() { ResourceID = resourceID, ImageType = ImageEntityType.Primary, LanguageCode = language, Rating = rating, RatingVotes = rating is null ? null : 3 };

    private static MetadataImageReconciler Reconciler(FakeImages images)
        => new(images.Manager.Object, new MetadataEntryLocks(), NullLogger<MetadataImageReconciler>.Instance);

    #endregion

    #region Desired and ordering

    [Fact]
    public async Task ThePreferredLanguagesComeFirstAndOnlyTheFirstFewAreDesired()
    {
        var images = new FakeImages();
        var settings = new MetadataImageSettings { MaxAutoPosters = 2, InternalImageLanguageOrder = ["en", "ja"] };

        var linked = await Reconciler(images).Reconcile(Entity(MetadataEntityType.Series), Source, [
            Poster("fr.jpg", "fr"),
            Poster("ja.jpg", "ja"),
            Poster("en.jpg", "en"),
            Poster("en-2.jpg", "en"),
        ], settings, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(4, linked);
        Assert.Equal(0, images.Of("en.jpg").Ordering);
        Assert.Equal(1, images.Of("en-2.jpg").Ordering);
        Assert.Equal(2, images.Of("ja.jpg").Ordering);
        Assert.Equal(3, images.Of("fr.jpg").Ordering);
        Assert.True(images.Of("en.jpg").IsDesired);
        Assert.True(images.Of("en-2.jpg").IsDesired);
        Assert.False(images.Of("ja.jpg").IsDesired);
        Assert.False(images.Of("fr.jpg").IsDesired);
        Assert.Equal(1, images.Scheduled);
    }

    [Fact]
    public async Task TheStoredDefaultTakesTheFirstSlotInAnyLanguage()
    {
        var images = new FakeImages();
        var settings = new MetadataImageSettings { MaxAutoPosters = 2, InternalImageLanguageOrder = ["en"] };

        await Reconciler(images).Reconcile(Entity(MetadataEntityType.Movie, "de.jpg"), Source, [
            Poster("en.jpg", "en"),
            Poster("en-2.jpg", "en"),
            Poster("de.jpg", "de"),
        ], settings, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([true, true, false], new[] { "de.jpg", "en.jpg", "en-2.jpg" }.Select(image => images.Of(image).IsDesired));
    }

    [Fact]
    public async Task TheStoredDefaultKeepsItsPlaceInTheLanguageOrder()
    {
        var images = new FakeImages();
        var settings = new MetadataImageSettings { MaxAutoPosters = 1, InternalImageLanguageOrder = ["en"] };

        await Reconciler(images).Reconcile(Entity(MetadataEntityType.Movie, "de.jpg"), Source, [
            Poster("fr.jpg", "fr"),
            Poster("de.jpg", "de"),
            Poster("en.jpg", "en"),
        ], settings, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([0, 1, 2], new[] { "en.jpg", "fr.jpg", "de.jpg" }.Select(image => images.Of(image).Ordering));
    }

    [Fact]
    public async Task MainStandsForTheOriginalLanguage()
    {
        var images = new FakeImages();
        var settings = new MetadataImageSettings { MaxAutoPosters = 1, InternalImageLanguageOrder = ["x-main", "en"] };

        await Reconciler(images).Reconcile(
            Entity(MetadataEntityType.Series),
            Source,
            [Poster("en.jpg", "en"), Poster("ja.jpg", "ja")],
            settings,
            originalLanguageCode: "ja",
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.True(images.Of("ja.jpg").IsDesired);
        Assert.False(images.Of("en.jpg").IsDesired);
        Assert.Equal(
            [TitleLanguage.Japanese, TitleLanguage.English],
            MetadataImageReconciler.GetLanguages(settings, "ja"));
        Assert.Equal([TitleLanguage.English], MetadataImageReconciler.GetLanguages(settings, null));
    }

    [Fact]
    public async Task EpisodesFollowTheThumbnailSetting()
    {
        var images = new FakeImages();
        var settings = new MetadataImageSettings { MaxAutoBackdrops = 5, MaxAutoThumbnails = 1, InternalImageLanguageOrder = [] };

        await Reconciler(images).Reconcile(Entity(MetadataEntityType.Episode), Source, [
            new() { ResourceID = "a.jpg", ImageType = ImageEntityType.Backdrop },
            new() { ResourceID = "b.jpg", ImageType = ImageEntityType.Backdrop },
        ], settings, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(images.Of("a.jpg", ImageEntityType.Backdrop).IsDesired);
        Assert.False(images.Of("b.jpg", ImageEntityType.Backdrop).IsDesired);
    }

    [Fact]
    public async Task APersonsImagesCountInEveryLanguage()
    {
        var images = new FakeImages();
        var settings = new MetadataImageSettings { MaxAutoStaffImages = 2, InternalImageLanguageOrder = ["en"] };

        await Reconciler(images).Reconcile(
            Entity(MetadataEntityType.Creator),
            Source,
            [Poster("ja.jpg", "ja"), Poster("fr.jpg", "fr"), Poster("de.jpg", "de")],
            settings,
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.True(images.Of("ja.jpg").IsDesired);
        Assert.True(images.Of("fr.jpg").IsDesired);
        Assert.False(images.Of("de.jpg").IsDesired);
        Assert.Equal([0, 1, 2], new[] { "ja.jpg", "fr.jpg", "de.jpg" }.Select(image => images.Of(image).Ordering));
    }

    [Fact]
    public async Task ALinkMadeSinceItWasReadIsUpdatedInstead()
    {
        var images = new FakeImages();
        var settings = new MetadataImageSettings { MaxAutoStaffImages = 1, InternalImageLanguageOrder = [] };
        images.Manager.Setup(m => m.AddImageCrossReference(It.IsAny<IWithImages>(), It.IsAny<IImage>(), It.IsAny<ImageCrossReferenceData>()))
            .Returns((IWithImages entity, IImage image, ImageCrossReferenceData data) => throw new ImageCrossReferenceExistsException
            {
                CrossReference = images.Add(new() { ImageID = image.ID, ImageType = data.ImageType, Source = data.Source, Ordering = 5 }).Object,
                Image = image,
                Entity = entity,
            });

        var linked = await Reconciler(images).Reconcile(
            Entity(MetadataEntityType.Character),
            Source,
            [Poster("a.jpg")],
            settings,
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Equal(1, linked);
        Assert.Equal(0, images.Of("a.jpg").Ordering);
        Assert.True(images.Of("a.jpg").IsDesired);
    }

    [Fact]
    public async Task ASecondLinkToTheSameImageIsRemoved()
    {
        var images = new FakeImages();
        await Reconciler(images).Reconcile(
            Entity(MetadataEntityType.Studio),
            Source,
            [Poster("a.jpg")],
            new(),
            cancellationToken: TestContext.Current.CancellationToken
        );
        var imageID = images.Of("a.jpg").ImageID;
        images.Add(new() { ImageID = imageID, ImageType = ImageEntityType.Primary, Source = Source, Ordering = 1 });

        await Reconciler(images).Reconcile(
            Entity(MetadataEntityType.Studio),
            Source,
            [Poster("a.jpg")],
            new(),
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Single(images.Xrefs, xref => xref.ImageID == imageID);
    }

    [Fact]
    public async Task ASharedEntityIsReconciledOneJobAtATime()
    {
        var images = new FakeImages();
        var locks = new MetadataEntryLocks();
        var reconciler = new MetadataImageReconciler(images.Manager.Object, locks, NullLogger<MetadataImageReconciler>.Instance);
        var creator = Entity(MetadataEntityType.Creator);

        Task<int> reconciling;
        using (await locks.Acquire(creator.ID, TestContext.Current.CancellationToken))
        {
            // Unblocked, the reconcile would finish before it returns.
            reconciling = reconciler.Reconcile(creator, Source, [Poster("a.jpg")], new(), cancellationToken: TestContext.Current.CancellationToken);
            Assert.False(reconciling.IsCompleted);
            Assert.Empty(images.Xrefs);
        }

        Assert.Equal(1, await reconciling);
    }

    [Fact]
    public async Task ASharedEntityLockedByTheCallerIsReconciledWithoutWaiting()
    {
        var images = new FakeImages();
        var locks = new MetadataEntryLocks();
        var reconciler = new MetadataImageReconciler(images.Manager.Object, locks, NullLogger<MetadataImageReconciler>.Instance);
        var creator = Entity(MetadataEntityType.Creator);

        using (await locks.Acquire(creator.ID, TestContext.Current.CancellationToken))
        {
            var reconciling = reconciler.Reconcile(
                creator,
                Source,
                [Poster("a.jpg")],
                new(),
                entityLocked: true,
                cancellationToken: TestContext.Current.CancellationToken
            );
            var linked = await reconciling.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.Equal(1, linked);
        }
    }

    [Fact]
    public async Task TheRatingIsKeptWhenInRange()
    {
        var images = new FakeImages();

        await Reconciler(images).Reconcile(
            Entity(MetadataEntityType.Series),
            Source,
            [Poster("a.jpg", rating: 7.5), Poster("b.jpg", rating: 0.5)],
            new(),
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Equal(7.5, images.Of("a.jpg").Rating);
        Assert.Null(images.Of("b.jpg").Rating);
    }

    #endregion

    #region Settings and removal

    [Theory]
    [InlineData(ImageEntityType.Primary)]
    [InlineData(ImageEntityType.Banner)]
    public async Task ATypeTurnedOffIsLeftAlone(ImageEntityType type)
    {
        var images = new FakeImages();
        var existing = images.Add(new() { ImageID = IImageManager.GetIDForImageSourceAndResourceID(Source, "old.jpg"), ImageType = type, Source = Source });
        var settings = new MetadataImageSettings { AutoDownloadPosters = type is not ImageEntityType.Primary, AutoDownloadBanners = type is not ImageEntityType.Banner };

        var linked = await Reconciler(images).Reconcile(Entity(MetadataEntityType.Series), Source, [
            new() { ResourceID = "new.jpg", ImageType = type },
        ], settings, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, linked);
        Assert.Equal([existing], images.Xrefs);
        Assert.Empty(images.Added);
    }

    [Fact]
    public async Task ATypeNoSettingCoversIsLinkedButNotDesired()
    {
        var images = new FakeImages();

        await Reconciler(images).Reconcile(Entity(MetadataEntityType.Series), Source, [
            new() { ResourceID = "disc.png", ImageType = ImageEntityType.Disc },
        ], new(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(images.Of("disc.png", ImageEntityType.Disc).IsDesired);
    }

    [Fact]
    public async Task AnImageTheSourceDroppedIsUnlinked()
    {
        var images = new FakeImages();
        var reconciler = Reconciler(images);
        var entity = Entity(MetadataEntityType.Series);
        await reconciler.Reconcile(
            entity,
            Source,
            [Poster("a.jpg"), Poster("b.jpg"), new() { ResourceID = "logo.png", ImageType = ImageEntityType.Logo }],
            new(),
            cancellationToken: TestContext.Current.CancellationToken
        );
        var other = images.Add(new() { ImageID = Guid.NewGuid(), ImageType = ImageEntityType.Primary, Source = MetadataSource.User });

        await reconciler.Reconcile(entity, Source, [Poster("b.jpg")], new(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, images.Of("b.jpg").Ordering);
        Assert.DoesNotContain(images.Xrefs, xref => xref.ImageID == IImageManager.GetIDForImageSourceAndResourceID(Source, "a.jpg"));
        Assert.DoesNotContain(images.Xrefs, xref => xref.ImageType == ImageEntityType.Logo);
        Assert.Contains(other, images.Xrefs);
    }

    [Fact]
    public async Task ACandidateThatCannotBeStoredIsSkipped()
    {
        var images = new FakeImages();

        var linked = await Reconciler(images).Reconcile(Entity(MetadataEntityType.Series), Source, [
            Poster(new string('a', MetadataImageReconciler.MaxResourceIDLength + 1)),
            Poster(" "),
            Poster("ok.jpg", language: "toolong-code"),
        ], new(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, linked);
        Assert.Null(Assert.Single(images.Added).LanguageCode);
    }

    [Fact]
    public async Task NothingIsLinkedWithoutATemplate()
    {
        var images = new FakeImages { Template = null };

        var linked = await Reconciler(images).Reconcile(
            Entity(MetadataEntityType.Series),
            Source,
            [Poster("a.jpg")],
            new(),
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Equal(0, linked);
        Assert.Empty(images.Xrefs);
        Assert.Equal(0, images.Scheduled);
    }

    [Fact]
    public void TheSourcesOwnSettingsWinOverTheDefaults_AndAnEntryWithoutThemUsesTheDefaults()
    {
        var settings = new MetadataSettings();
        var own = new MetadataImageSettings { AutoDownloadLogos = false };
        settings.Sources.Add(new() { Source = Source, Images = own });
        settings.Sources.Add(new() { Source = TestSources.AniList, EpisodeMatchLookAheadDays = 1 });

        Assert.Same(own, settings.GetImageSettings(Source));
        Assert.Same(settings.SourceDefaults.Images, settings.GetImageSettings(TestSources.AniList));
        Assert.NotEmpty(MetadataSourceOverrides.Validate(new() { Source = MetadataSource.AniDB }));
        Assert.Empty(MetadataSourceOverrides.Validate(new() { Source = MetadataSource.TMDB }));
    }

    [Fact]
    public void NetworksGoByTheStudioSwitch_AndAKindWithNothingOnIsLeftOut()
    {
        var settings = new MetadataImageSettings { AutoDownloadStudioImages = false };

        Assert.False(settings.GetRule(MetadataEntityType.Network, ImageEntityType.Logo)?.Enabled);
        Assert.True(new MetadataImageSettings().GetRule(MetadataEntityType.Network, ImageEntityType.Logo)?.Enabled);
        Assert.False(settings.AnyEnabledFor(MetadataEntityType.Network));
        Assert.True(settings.AnyEnabledFor(MetadataEntityType.Series));
    }

    #endregion
}
