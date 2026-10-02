using System;
using System.IO;
using System.Threading.Tasks;
using ImageMagick;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Purges images against the migrated database and checks what is left on
/// disk: the image's own file goes with it, adding the image again does not
/// pick up an old file, and an orphan purge of one source spares the others.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ImagePurgeRoundTripTests(DatabaseMigrationFixture fixture)
{
    private const string Template = "https://images.example.com/{0}";

    /// <summary>
    /// Throws the image caches away and reads them again from the database.
    /// </summary>
    private void Reload()
    {
        fixture.Services.GetRequiredService<ShokoImageRepository>().Populate(displayName: false);
        fixture.Services.GetRequiredService<ShokoImage_EntityRepository>().Populate(displayName: false);
    }

    /// <summary>
    /// A small PNG of one colour.
    /// </summary>
    private static byte[] Png(MagickColor color)
    {
        using var image = new MagickImage(color, 8, 8) { Format = MagickFormat.Png };
        return image.ToByteArray();
    }

    [Fact]
    public async Task APurgedImagesFileGoes_AndAddingItAgainDoesNotPickItUp()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var images = fixture.Services.GetRequiredService<IImageManager>();
        var files = fixture.Services.GetRequiredService<IImageFileStore>();
        images.RegisterTemplateUrl(TestSources.Image, Template);
        var resourceID = $"purge/{Guid.NewGuid():N}.png";

        var image = images.AddImage(new() { Source = TestSources.Image, ResourceID = resourceID });
        files.StoreFile(image, Png(MagickColors.Green));
        var held = images.GetImageByID(image.ID)!.LocalPath;
        Assert.True(File.Exists(held));

        Assert.True(await images.PurgeImage(image));
        Reload();

        Assert.Null(images.GetImageByID(image.ID));
        Assert.False(File.Exists(held));

        var again = images.AddImage(new() { Source = TestSources.Image, ResourceID = resourceID });
        Assert.False(again.IsAvailable);
        Assert.Null(again.GetStream());
        Assert.True(await images.PurgeImage(again));
        Assert.False(await images.PurgeImage(again));
    }

    [Fact]
    public async Task PurgingOrphanedImagesOfOneSource_LeavesOtherSourcesAlone()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var images = fixture.Services.GetRequiredService<IImageManager>();
        var files = fixture.Services.GetRequiredService<IImageFileStore>();
        images.RegisterTemplateUrl(TestSources.Image, Template);

        var remote = images.AddImage(new() { Source = TestSources.Image, ResourceID = $"orphans/{Guid.NewGuid():N}.png" });
        files.StoreFile(remote, Png(MagickColors.Yellow));
        var remotePath = images.GetImageByID(remote.ID)!.LocalPath;
        var uploaded = images.UploadImage(Png(MagickColors.Purple));
        var uploadedPath = uploaded.LocalPath;

        var purged = await images.PurgeOrphanedImages(daysOld: 0, imageSource: TestSources.Image, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(purged >= 1);
        Reload();

        Assert.Null(images.GetImageByID(remote.ID));
        Assert.False(File.Exists(remotePath));
        Assert.NotNull(images.GetImageByID(uploaded.ID));
        Assert.True(File.Exists(uploadedPath));

        Assert.True(await images.PurgeImage(uploaded));
        Assert.False(File.Exists(uploadedPath));
    }
}
