using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Utilities;
using Shoko.Server.Databases;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;
using Xunit;

#pragma warning disable CS0618
namespace Shoko.IntegrationTests;

/// <summary>
/// Runs the image data fixes against the migrated database on each backend:
/// the rewrite of the IDs older versions hashed from the old enum spelling,
/// and the availability flag set from the files on disk.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ImageIdentityMigrationTests(DatabaseMigrationFixture fixture)
{
    private static Guid Hash(string sourceText, string resourceID)
        => UuidUtility.GetV5($"ImageSource={sourceText},ResourceID={resourceID}", IImageManager.ImageIdentifierNamespace);

    private static ShokoImage Image(Guid id, Guid primaryID, string resourceID)
        => new()
        {
            ID = id,
            PrimaryID = primaryID,
            Source = MetadataSource.TMDB,
            ResourceID = resourceID,
            ContentType = "image/jpeg",
            CreatedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow,
        };

    [Fact]
    public void TheImageStepRewritesOldImageIDs_AndASecondRunChangesNothing()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var images = fixture.Services.GetRequiredService<ShokoImageRepository>();
        var xrefs = fixture.Services.GetRequiredService<ShokoImage_EntityRepository>();
        var primaryResource = $"/primary-{Guid.NewGuid():N}.jpg";
        var linkedResource = $"/linked-{Guid.NewGuid():N}.jpg";
        var primaryOldID = Hash("TMDB", primaryResource);
        var linkedOldID = Hash("TMDB", linkedResource);
        var primaryNewID = IImageManager.GetIDForImageSourceAndResourceID(MetadataSource.TMDB, primaryResource);
        var linkedNewID = IImageManager.GetIDForImageSourceAndResourceID(MetadataSource.TMDB, linkedResource);

        images.Save(Image(primaryOldID, primaryOldID, primaryResource));
        images.Save(Image(linkedOldID, primaryOldID, linkedResource));
        var xref = new ShokoImage_Entity
        {
            ImageID = linkedOldID,
            PrimaryImageID = primaryOldID,
            ImageType = ImageEntityType.Backdrop,
            ImageSource = MetadataSource.TMDB,
            EntitySource = MetadataSource.TMDB,
            EntityType = MetadataEntityType.Series,
            EntityID = "1",
            IsEnabled = true,
            Source = MetadataSource.TMDB,
            CreatedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow,
        };
        xrefs.Save(xref);
        var localIDs = new[] { images.GetByID(primaryOldID)!.LocalID, images.GetByID(linkedOldID)!.LocalID };

        // Only the primary image has a file, in the old folder under its old ID.
        var imagesPath = ApplicationPaths.Instance.ImagesPath;
        var oldFile = Path.Join(imagesPath, "TMDB", primaryOldID.ToString("N")[..2], primaryOldID.ToString("N") + ".jpg");
        var newFile = Path.Join(imagesPath, "tmdb", primaryNewID.ToString("N")[..2], primaryNewID.ToString("N") + ".jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(oldFile)!);
        File.WriteAllText(oldFile, "poster");

        DatabaseFixes.MoveImagesToSourceValueFolders();
        images.Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);
        xrefs.Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(images.GetByID(primaryOldID));
        Assert.Null(images.GetByID(linkedOldID));
        var primary = images.GetByID(primaryNewID);
        var linked = images.GetByID(linkedNewID);
        Assert.NotNull(primary);
        Assert.NotNull(linked);
        Assert.Equal(primaryNewID, primary.PrimaryID);
        Assert.Equal(primaryNewID, linked.PrimaryID);
        Assert.Equal(localIDs, new[] { primary.LocalID, linked.LocalID });
        var stored = xrefs.GetByID(xref.ID);
        Assert.NotNull(stored);
        Assert.Equal(linkedNewID, stored.ImageID);
        Assert.Equal(primaryNewID, stored.PrimaryImageID);
        Assert.Equal("poster", File.ReadAllText(newFile));
        Assert.Empty(Directory.EnumerateFiles(imagesPath, primaryOldID.ToString("N") + "*", SearchOption.AllDirectories));

        DatabaseFixes.MoveImagesToSourceValueFolders();
        images.Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);
        xrefs.Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([primaryNewID, linkedNewID], images.GetByPrimaryImageID(primaryNewID).Select(image => image.ID).OrderBy(id => id != primaryNewID));
        Assert.Equal([linkedNewID], xrefs.GetByPrimaryImageID(primaryNewID).Select(entity => entity.ImageID));
        Assert.Equal("poster", File.ReadAllText(newFile));
    }

    [Fact]
    public void AnOldRowReplacesARowAlreadyStoredUnderItsNewID()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var images = fixture.Services.GetRequiredService<ShokoImageRepository>();
        var resource = $"/replaced-{Guid.NewGuid():N}.jpg";
        var oldID = Hash("TMDB", resource);
        var newID = IImageManager.GetIDForImageSourceAndResourceID(MetadataSource.TMDB, resource);
        var oldImage = Image(oldID, oldID, resource);
        oldImage.Width = 100;
        var newImage = Image(newID, newID, resource);
        newImage.Width = 200;
        images.Save(oldImage);
        images.Save(newImage);
        var localID = images.GetByID(oldID)!.LocalID;

        DatabaseFixes.MoveImagesToSourceValueFolders();
        for (var run = 0; run < 2; run++)
        {
            if (run is 1)
                images.Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Null(images.GetByID(oldID));
            var stored = images.GetByID(newID);
            Assert.NotNull(stored);
            Assert.Equal(100, stored.Width);
            Assert.Equal(localID, stored.LocalID);
            Assert.Equal(newID, stored.PrimaryID);
        }
    }

    [Fact]
    public void TheAvailabilityStep_SetsTheFlagFromTheFiles_AndUpdatesTheCache()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var images = fixture.Services.GetRequiredService<ShokoImageRepository>();
        ShokoImage Seed(string name, bool isAvailable, byte[]? content)
        {
            var resource = $"/{name}-{Guid.NewGuid():N}.jpg";
            var id = IImageManager.GetIDForImageSourceAndResourceID(MetadataSource.TMDB, resource);
            var image = Image(id, id, resource);
            image.IsAvailable = isAvailable;
            images.Save(image);
            if (content is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(image.LocalPath)!);
                File.WriteAllBytes(image.LocalPath, content);
            }

            return image;
        }

        var valid = Seed("valid", false, [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1]);
        var invalid = Seed("invalid", true, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]);
        var missing = Seed("missing", true, null);

        DatabaseFixes.PopulateImageAvailability();

        Assert.True(images.GetByID(valid.ID)!.IsAvailable);
        Assert.False(images.GetByID(invalid.ID)!.IsAvailable);
        Assert.False(images.GetByID(missing.ID)!.IsAvailable);
        images.Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(images.GetByID(valid.ID)!.IsAvailable);
        Assert.False(images.GetByID(invalid.ID)!.IsAvailable);
        Assert.False(images.GetByID(missing.ID)!.IsAvailable);
    }
}
