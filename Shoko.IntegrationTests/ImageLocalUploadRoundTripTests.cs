using System;
using System.IO;
using System.Threading.Tasks;
using ImageMagick;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Repositories.Cached;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Uploads images under local sources against the migrated database: a
/// plugin's own local source keeps its images apart from the user's.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ImageLocalUploadRoundTripTests(DatabaseMigrationFixture fixture)
{
    /// <summary>
    /// Throws the image cache away and reads it again from the database.
    /// </summary>
    private void Reload()
        => fixture.Services.GetRequiredService<ShokoImageRepository>().Populate(displayName: false);

    /// <summary>
    /// A small PNG of one colour.
    /// </summary>
    private static byte[] Png(MagickColor color)
    {
        using var image = new MagickImage(color, 8, 8) { Format = MagickFormat.Png };
        return image.ToByteArray();
    }

    [Fact]
    public async Task AnImageUploadedUnderAPluginsLocalSource_IsKeptUnderIt()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var images = fixture.Services.GetRequiredService<IImageManager>();
        var file = Png(MagickColors.Orange);

        var local = images.UploadImage(file, "image/png", TestSources.LocalImage);
        var fromStream = images.UploadImage(new MemoryStream(file), null, TestSources.LocalImage);
        var asUser = images.UploadImage(file, null, MetadataSource.User);
        Reload();

        var stored = images.GetImageByID(local.ID);
        Assert.NotNull(stored);
        Assert.Equal((TestSources.LocalImage, "image/png", true), (stored.Source, stored.ContentType, stored.IsAvailable));
        Assert.Contains(Path.DirectorySeparatorChar + TestSources.LocalImage.Value + Path.DirectorySeparatorChar, stored.LocalPath);
        Assert.True(File.Exists(stored.LocalPath));
        Assert.Equal(local.ID, fromStream.ID);
        Assert.NotEqual(local.ID, asUser.ID);
        Assert.Equal(MetadataSource.User, asUser.Source);

        Assert.True(await images.PurgeImage(local));
        Assert.True(await images.PurgeImage(asUser));
    }
}
