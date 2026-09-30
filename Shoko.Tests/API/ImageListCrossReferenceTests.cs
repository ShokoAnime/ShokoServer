using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Image.Options;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.ImageManagement;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Covers the links APIv3 hands out with the image lists of an entity: every
/// link the entity sees an image through, its own first, while the image-wide
/// fields keep their meaning.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class ImageListCrossReferenceTests
{
    #region Harness

    private static readonly MetadataGuid _shokoSeasonID = new(MetadataSource.Shoko, MetadataEntityType.Season, "1");

    private static readonly MetadataGuid _anidbSeasonID = new(MetadataSource.AniDB, MetadataEntityType.Season, "10");

    private static readonly Guid _imageA = Guid.Parse("0a000000-0000-0000-0000-000000000000");

    private static readonly Guid _imageB = Guid.Parse("0b000000-0000-0000-0000-000000000000");

    /// <summary>
    /// Installs the images and links, and builds the image manager over them.
    /// </summary>
    /// <param name="xrefs">The stored links.</param>
    /// <returns>The scope to dispose and the image manager.</returns>
    private static (RepoFactoryScope Scope, ImageManager Manager) Harness(params ShokoImage_Entity[] xrefs)
    {
        var xrefRepository = CachedRepo.Build<ShokoImage_EntityRepository, int, ShokoImage_Entity>(xref => xref.ID, xrefs);
        var imageRepository = CachedRepo.Build<ShokoImageRepository, Guid, ShokoImage>(image => image.ID, [StoredImage(_imageA), StoredImage(_imageB)]);
        var scope = new RepoFactoryScope().Set(xrefRepository).Set(imageRepository);
        var constructor = typeof(ImageManager).GetConstructors().Single();
        var arguments = constructor.GetParameters()
            .Select(parameter => parameter.ParameterType switch
            {
                var type when type == typeof(ILogger<ImageManager>) => new Mock<ILogger<ImageManager>>().Object,
                var type when type == typeof(ShokoImage_EntityRepository) => xrefRepository,
                var type when type == typeof(ShokoImageRepository) => imageRepository,
                _ => (object?)null,
            })
            .ToArray();
        return (scope, (ImageManager)constructor.Invoke(arguments));
    }

    private static ShokoImage StoredImage(Guid id)
        => new() { ID = id, PrimaryID = id, Source = MetadataSource.TMDB, ResourceID = $"/{id}.jpg", IsAvailable = true };

    private static ShokoImage_Entity Xref(int id, Guid imageID, MetadataGuid entityID, ImageEntityType type = ImageEntityType.Primary, bool enabled = true, bool preferred = false)
    {
        var entity = new Mock<IWithImages>();
        entity.SetupGet(e => e.ID).Returns(entityID);
        var data = new ImageCrossReferenceData
        {
            ImageType = type,
            Source = entityID.Source,
            IsEnabled = enabled,
            IsDesired = true,
            IsPreferred = preferred,
            Ordering = id,
        };
        return new(StoredImage(imageID), entity.Object, data, 0) { ID = id };
    }

    /// <summary>
    /// A shoko season linked to one AniDB season.
    /// </summary>
    private static IShokoSeason Season()
    {
        var anidbSeason = new Mock<ISeason>();
        anidbSeason.SetupGet(s => s.ID).Returns(_anidbSeasonID);
        var season = new Mock<IShokoSeason>();
        season.SetupGet(s => s.ID).Returns(_shokoSeasonID);
        season.SetupGet(s => s.LinkedSeasons).Returns([anidbSeason.Object]);
        return season.Object;
    }

    /// <summary>
    /// Lists the images of an entity the way the image list endpoints do.
    /// </summary>
    private static Images List(ImageManager manager, IWithImages entity, ImageFilteringOptions options)
        => manager.GetImagesForEntity(entity, options).ToDto().WithCrossReferences(manager.GetCrossReferencesForImageList(entity, options));

    #endregion

    #region Links

    [Fact]
    public void AnImageNamesEveryLinkTheEntitySeesItThrough_ItsOwnFirst()
    {
        var (scope, manager) = Harness(
            Xref(1, _imageA, _anidbSeasonID),
            Xref(2, _imageA, _shokoSeasonID, preferred: true),
            Xref(3, _imageB, _anidbSeasonID, preferred: true)
        );
        using var _ = scope;

        var posters = List(manager, Season(), new() { IsEnabled = true }).Posters;

        var imageA = Assert.Single(posters, image => image.UID == _imageA);
        Assert.Equal([2, 1], imageA.CrossReferences!.Select(link => link.ID));
        Assert.True(imageA.Preferred);
        Assert.Equal([true, false], imageA.CrossReferences!.Select(link => link.IsPreferred));
        var own = imageA.CrossReferences![0];
        Assert.Equal(MetadataSource.Shoko, own.EntitySource);
        Assert.Equal(MetadataEntityType.Season, own.EntityType);
        Assert.Equal("1", own.EntityID);

        // Preferred only on the AniDB season's own link, which the shoko season merely sees.
        var imageB = Assert.Single(posters, image => image.UID == _imageB);
        Assert.False(imageB.Preferred);
        var link = Assert.Single(imageB.CrossReferences!);
        Assert.Equal(3, link.ID);
        Assert.True(link.IsPreferred);
        Assert.Equal(MetadataSource.AniDB, link.EntitySource);
        Assert.Equal("10", link.EntityID);
    }

    [Fact]
    public void ADisabledLinkIsNamedOnlyWhenDisabledImagesAreAsked()
    {
        var (scope, manager) = Harness(
            Xref(1, _imageA, _anidbSeasonID),
            Xref(2, _imageA, _shokoSeasonID, enabled: false)
        );
        using var _ = scope;

        var enabledOnly = Assert.Single(List(manager, Season(), new() { IsEnabled = true }).Posters);
        Assert.False(enabledOnly.Disabled);
        Assert.Equal([1], enabledOnly.CrossReferences!.Select(link => link.ID));

        var all = Assert.Single(List(manager, Season(), new()).Posters);
        Assert.False(all.Disabled);
        Assert.Equal([2, 1], all.CrossReferences!.Select(link => link.ID));
        Assert.Equal([false, true], all.CrossReferences!.Select(link => link.IsEnabled));
    }

    [Fact]
    public void AnImageDisabledOnEveryLinkStaysDisabledImageWide()
    {
        var (scope, manager) = Harness(
            Xref(1, _imageA, _anidbSeasonID, enabled: false),
            Xref(2, _imageA, _shokoSeasonID, enabled: false)
        );
        using var _ = scope;

        var image = Assert.Single(List(manager, Season(), new()).Posters);

        Assert.True(image.Disabled);
        Assert.All(image.CrossReferences!, link => Assert.False(link.IsEnabled));
    }

    [Fact]
    public void LinksAreMatchedByImageType()
    {
        var (scope, manager) = Harness(
            Xref(1, _imageA, _shokoSeasonID),
            Xref(2, _imageA, _shokoSeasonID, ImageEntityType.Backdrop)
        );
        using var _ = scope;

        var images = List(manager, Season(), new());

        Assert.Equal(1, Assert.Single(Assert.Single(images.Posters).CrossReferences!).ID);
        Assert.Equal(2, Assert.Single(Assert.Single(images.Backdrops).CrossReferences!).ID);
    }

    [Fact]
    public void APageOfImagesAndASingleImageGetTheirLinksToo()
    {
        var (scope, manager) = Harness(
            Xref(1, _imageA, _anidbSeasonID),
            Xref(2, _imageA, _shokoSeasonID)
        );
        using var _ = scope;
        var season = Season();
        var options = new ImageFilteringOptions { ImageType = ImageEntityType.Primary };
        var xrefs = manager.GetCrossReferencesForImageList(season, options);

        var page = manager.GetImagesForEntity(season, options).ToListResult(image => new Image(image), 1, 10).WithCrossReferences(xrefs);
        var single = new Image(manager.GetImagesForEntity(season, options)[0]).WithCrossReferences(xrefs);

        Assert.Equal([2, 1], Assert.Single(page.List).CrossReferences!.Select(link => link.ID));
        Assert.Equal([2, 1], single.CrossReferences!.Select(link => link.ID));
    }

    #endregion

    #region Filters and output

    [Fact]
    public void TheLinksAreAskedForWithTheFiltersOfTheList()
    {
        var entity = new Mock<IWithImages>().Object;
        var imageManager = new Mock<IImageManager>();
        ImageCrossReferenceFilteringOptions? asked = null;
        imageManager
            .Setup(m => m.GetImageCrossReferencesForEntity(entity, It.IsAny<ImageCrossReferenceFilteringOptions?>()))
            .Callback<IWithImages, ImageCrossReferenceFilteringOptions?>((_, options) => asked = options)
            .Returns([]);

        imageManager.Object.GetCrossReferencesForImageList(entity, new() { ImageType = ImageEntityType.Logo, IsEnabled = true });

        // Every link the entity sees an image through is asked for, not only its own.
        Assert.NotNull(asked);
        Assert.Equal(ImageEntityType.Logo, asked.ImageType);
        Assert.True(asked.IsEnabled);
        Assert.Null(asked.EntitySource);
        Assert.Null(asked.EntityType);
    }

    [Fact]
    public void AnImageWithoutLinksLeavesThemOutOfItsJson()
    {
        var (scope, manager) = Harness(Xref(1, _imageA, _shokoSeasonID));
        using var _ = scope;
        var image = manager.GetImagesForEntity(Season())[0];

        var plain = JObject.Parse(JsonConvert.SerializeObject(new Image(image)));
        var linked = JObject.Parse(JsonConvert.SerializeObject(new Image(image).WithCrossReferences(manager.GetCrossReferencesForImageList(Season()))));

        Assert.False(plain.ContainsKey(nameof(Image.CrossReferences)));
        Assert.Equal(1, linked[nameof(Image.CrossReferences)]![0]![nameof(ImageCrossReferenceSlim.ID)]!.Value<int>());
    }

    #endregion
}
