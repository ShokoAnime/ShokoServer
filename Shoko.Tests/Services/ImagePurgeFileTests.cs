using System;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers the files <see cref="ImageManager"/> deletes when it purges an
/// image: every file named by the image's ID in its folder, and nothing else,
/// never outside the images folder.
/// </summary>
public sealed class ImagePurgeFileTests : IDisposable
{
    #region Harness

    private readonly string _root = Path.Join(Path.GetTempPath(), $"shoko-image-purge-{Guid.NewGuid():N}");

    private readonly string _imagesPath;

    public ImagePurgeFileTests()
    {
        _imagesPath = Path.Join(_root, "images");
        Directory.CreateDirectory(_imagesPath);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Left for the system to clean up.
        }
    }

    private static string Touch(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "image");
        return path;
    }

    private static int Delete(string imagesPath, string folder, Guid imageID)
        => ImageManager.DeleteHeldFiles(imagesPath, folder, imageID, NullLogger.Instance);

    #endregion

    #region Tests

    [Fact]
    public void EveryFileNamedByTheID_IsDeleted_AndNothingElse()
    {
        var imageID = Guid.NewGuid();
        var otherID = Guid.Parse(imageID.ToString("N")[..2] + Guid.NewGuid().ToString("N")[2..]);
        var id = imageID.ToString("N");
        var folder = ShokoImage.GetFolder(_imagesPath, MetadataSource.TMDB, imageID);
        var held = Touch(Path.Join(folder, id + ".png"));
        var oldFormat = Touch(Path.Join(folder, id + ".jpg"));
        var neighbour = Touch(Path.Join(folder, otherID.ToString("N") + ".png"));
        var lookalike = Touch(Path.Join(folder, id + "-copy.png"));
        var otherSource = Touch(Path.Join(ShokoImage.GetFolder(_imagesPath, MetadataSource.AniDB, imageID), id + ".png"));
        var outside = Touch(Path.Join(_root, id + ".png"));

        Assert.Equal(2, Delete(_imagesPath, folder, imageID));

        Assert.False(File.Exists(held));
        Assert.False(File.Exists(oldFormat));
        Assert.True(File.Exists(neighbour));
        Assert.True(File.Exists(lookalike));
        Assert.True(File.Exists(otherSource));
        Assert.True(File.Exists(outside));
    }

    [Fact]
    public void AMissingFileOrFolder_IsFine()
    {
        var imageID = Guid.NewGuid();
        var folder = ShokoImage.GetFolder(_imagesPath, MetadataSource.TMDB, imageID);

        Assert.Equal(0, Delete(_imagesPath, folder, imageID));

        Directory.CreateDirectory(folder);
        Assert.Equal(0, Delete(_imagesPath, folder, imageID));
    }

    [Fact]
    public void AFolderOutsideTheImagesFolder_IsNeverTouched()
    {
        var imageID = Guid.NewGuid();
        var id = imageID.ToString("N");
        var outsideFolder = Path.Join(_root, "outside", id[..2]);
        var outside = Touch(Path.Join(outsideFolder, id + ".png"));
        // A sibling whose name starts with the images folder's is still outside it.
        var siblingFolder = Path.Join(_imagesPath + "-sibling", id[..2]);
        var sibling = Touch(Path.Join(siblingFolder, id + ".png"));

        Assert.Equal(0, Delete(_imagesPath, Path.Join(_imagesPath, "..", "outside", id[..2]), imageID));
        Assert.Equal(0, Delete(_imagesPath, siblingFolder, imageID));
        Assert.Equal(0, Delete(_imagesPath, _root, imageID));

        Assert.True(File.Exists(outside));
        Assert.True(File.Exists(sibling));
    }

    #endregion
}
