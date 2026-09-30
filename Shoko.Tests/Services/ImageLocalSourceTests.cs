using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers how <see cref="ImageManager"/> treats local sources beyond the
/// user's: uploads refuse any source that is not a registered local one, and
/// the images of a plugin's locally made entries rank with the user's.
/// </summary>
public class ImageLocalSourceTests
{
    #region Harness

    /// <summary>
    /// Builds the image manager over the given cross-references and no
    /// images; every other dependency is left out, as neither refusing an
    /// upload nor ranking cross-references reaches it.
    /// </summary>
    /// <param name="xrefs">The stored cross-references.</param>
    /// <returns>The image manager.</returns>
    private static ImageManager Manager(params ShokoImage_Entity[] xrefs)
    {
        var repository = CachedRepo.Build<ShokoImage_EntityRepository, int, ShokoImage_Entity>(xref => xref.ID, xrefs);
        var images = CachedRepo.Build<ShokoImageRepository, Guid, ShokoImage>(image => image.ID);
        var constructor = typeof(ImageManager).GetConstructors().Single();
        var arguments = constructor.GetParameters()
            .Select(parameter => parameter.ParameterType switch
            {
                var type when type == typeof(ILogger<ImageManager>) => new Mock<ILogger<ImageManager>>().Object,
                var type when type == typeof(ShokoImage_EntityRepository) => repository,
                var type when type == typeof(ShokoImageRepository) => images,
                _ => (object?)null,
            })
            .ToArray();
        return (ImageManager)constructor.Invoke(arguments);
    }

    private static ISeason Season(MetadataGuid id)
    {
        var season = new Mock<ISeason>();
        season.SetupGet(s => s.Episodes).Returns([]);
        season.SetupGet(s => s.ID).Returns(id);
        return season.Object;
    }

    private static ShokoImage_Entity Xref(int id, ISeason season)
    {
        var image = new Mock<IImage>();
        image.SetupGet(i => i.ID).Returns(Guid.NewGuid());
        image.SetupGet(i => i.Source).Returns(MetadataSource.TMDB);
        return new(image.Object, season, new() { ImageType = ImageEntityType.Primary, Source = season.ID.Source, IsEnabled = true }, 0) { ID = id };
    }

    private static readonly byte[] _bytes = [1, 2, 3];

    #endregion

    #region Uploads

    [Fact]
    public void Upload_RefusesARemoteUnregisteredOrMissingSource()
    {
        var manager = Manager();

        // The bytes are no image either, so only the source's name tells the
        // source was refused first.
        foreach (var source in new[] { MetadataSource.TMDB, MetadataSource.AniDB, TestSources.Plugin, MetadataSource.Parse("image-local-source-tests-unregistered"), null! })
        {
            Assert.Equal("source", Assert.ThrowsAny<ArgumentException>(() => manager.UploadImage(_bytes, null, source)).ParamName);
            Assert.Equal("source", Assert.ThrowsAny<ArgumentException>(() => manager.UploadImage(new MemoryStream(_bytes), null, source)).ParamName);
        }
    }

    [Fact]
    public void Upload_TakesARegisteredLocalSourceAsFarAsTheImageData()
    {
        var manager = Manager();

        // Past the source check, the bytes are found not to be an image.
        foreach (var source in new[] { MetadataSource.User, MetadataSource.Generated, TestSources.LocalPlugin })
            Assert.Equal("imageByteArray", Assert.Throws<ArgumentException>(() => manager.UploadImage(_bytes, null, source)).ParamName);
    }

    #endregion

    #region Ranking

    [Fact]
    public void LinkedImages_OfLocallyMadeEntries_RankBeforeRemoteOnes_AndShokoKeepsItsPlace()
    {
        var seasonID = new MetadataGuid(MetadataSource.Shoko, MetadataEntityType.Season, "1");
        var linked = new[]
        {
            Season(new(MetadataSource.AniDB, MetadataEntityType.Season, "10")),
            Season(new(TestSources.Plugin, MetadataEntityType.Season, "20")),
            Season(new(TestSources.LocalPlugin, MetadataEntityType.Season, "30")),
            Season(new(MetadataSource.User, MetadataEntityType.Season, "40")),
            Season(new(MetadataSource.Shoko, MetadataEntityType.Season, "50")),
        };
        var manager = Manager([.. linked.Select((season, index) => Xref(index + 1, season))]);
        var season = new Mock<IShokoSeason>();
        season.SetupGet(s => s.ID).Returns(seasonID);
        season.SetupGet(s => s.LinkedSeasons).Returns(linked);

        var order = manager.GetImageCrossReferencesForEntity(season.Object).Select(xref => xref.EntityID.Source).ToList();

        Assert.Equal(5, order.Count);
        Assert.Equal([TestSources.LocalPlugin, MetadataSource.User], order.Take(2).Order());
        Assert.Equal([MetadataSource.AniDB, TestSources.Plugin, MetadataSource.Shoko], order.Skip(2));
    }

    #endregion
}
