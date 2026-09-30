using System;
using System.IO;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Utilities;
using Shoko.Server.Databases;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Databases;

/// <summary>
/// Unit tests for <see cref="ImageIdentityMigrator"/>, which plans the move
/// from image IDs hashed from the old enum spelling to IDs hashed from the
/// source value, and for the file moves that go with it.
/// </summary>
public sealed class ImageIdentityMigratorTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("shoko-image-ids-");

    public void Dispose()
        => _root.Delete(recursive: true);

    #region Helpers

    private static Guid Hash(string sourceText, string resourceID)
        => UuidUtility.GetV5($"ImageSource={sourceText},ResourceID={resourceID}", IImageManager.ImageIdentifierNamespace);

    // An image as a daily build stored it: under the ID hashed from the old spelling.
    private static ImageIdentity OldImage(MetadataSource source, string resourceID, Guid? primaryID = null)
    {
        var id = Hash(DatabaseFixes.GetOldSourceSpelling(source.Value)!, resourceID);
        return new(id, primaryID ?? id, source, resourceID);
    }

    // An image as a new version stores it: under the ID hashed from the value.
    private static ImageIdentity NewImage(MetadataSource source, string resourceID)
    {
        var id = ImageIdentityMigrator.GetNewID(source, resourceID);
        return new(id, id, source, resourceID);
    }

    private string Write(string folder, string id, string extension, string content = "image")
    {
        var path = PathOf(folder, id, extension);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private string PathOf(string folder, string id, string extension)
        => Path.Join(_root.FullName, folder, id[..2], id + extension);

    // Moves an image's file the way the image step does.
    private static ImageMoveResult MoveFile(ImageCacheMigrator migrator, ImageIdentity image, string extension)
    {
        var files = migrator.FindFiles(image.Source.Value, ImageCacheMigrator.GetOldFolderNames(image.Source), ImageIdentityMigrator.GetFileIDs(image));
        return migrator.MoveToTarget(migrator.GetTargetPath(image.Source.Value, image.NewID.ToString("N"), extension), files);
    }

    #endregion

    #region IDs

    [Fact]
    public void GetFileIDs_ListsTheNewThenTheStoredID()
    {
        var other = Guid.NewGuid();
        var image = new ImageIdentity(other, other, MetadataSource.AniDB, "/a.jpg");

        Assert.Equal([image.NewID.ToString("N"), other.ToString("N")], ImageIdentityMigrator.GetFileIDs(image));
    }

    [Fact]
    public void GetFileIDs_ListsEachIDOnce()
    {
        Assert.Equal(2, ImageIdentityMigrator.GetFileIDs(OldImage(MetadataSource.TMDB, "/a.jpg")).Count);
        Assert.Single(ImageIdentityMigrator.GetFileIDs(NewImage(TestSources.Plugin, "/a.jpg")));
    }

    #endregion

    #region ID Map

    [Fact]
    public void MapID_LeavesAnUnknownIDAlone()
    {
        var unknown = Guid.NewGuid();

        Assert.Equal(unknown, ImageIdentityMigrator.MapID(ImageIdentityMigrator.BuildIDMap([OldImage(MetadataSource.AniDB, "/a.jpg")]), unknown));
    }

    [Fact]
    public void PlanPrimaryIDs_MapsAPrimaryIDToTheNewIDOfTheImageItPointsAt()
    {
        var primary = OldImage(MetadataSource.TMDB, "/primary.jpg");
        var linked = OldImage(MetadataSource.TMDB, "/linked.jpg", primary.ID);
        var images = new[] { primary, linked };

        var plan = ImageIdentityMigrator.PlanPrimaryIDs(images, ImageIdentityMigrator.BuildIDMap(images));

        Assert.Equal([(linked.ID, primary.NewID)], plan);
    }

    [Fact]
    public void GetPrimaryIDAfterRename_GivesAnImageThatIsItsOwnPrimaryItsNewID()
    {
        var image = OldImage(MetadataSource.AniDB, "/a.jpg");
        var primaryID = Guid.NewGuid();

        Assert.Equal(image.NewID, ImageIdentityMigrator.GetPrimaryIDAfterRename(image.ID, image.PrimaryID, image.NewID));
        Assert.Equal(primaryID, ImageIdentityMigrator.GetPrimaryIDAfterRename(image.ID, primaryID, image.NewID));
    }

    [Fact]
    public void PlanPrimaryIDs_IsEmptyWhenEveryImageHasItsNewID()
    {
        var images = new[] { NewImage(MetadataSource.AniDB, "/a.jpg"), NewImage(MetadataSource.User, "abc") };

        Assert.Empty(ImageIdentityMigrator.PlanPrimaryIDs(images, ImageIdentityMigrator.BuildIDMap(images)));
    }

    #endregion

    #region Row Changes

    [Fact]
    public void PlanRowChanges_RenamesAnOldRowAndLeavesANewRow()
    {
        var old = OldImage(MetadataSource.AniDB, "/a.jpg");
        var current = NewImage(MetadataSource.TMDB, "/b.jpg");

        Assert.Equal([new ImageIDChange(old.ID, old.NewID)], ImageIdentityMigrator.PlanRowChanges([old, current]));
    }

    #endregion

    #region Files

    [Fact]
    public void ADailyBuildFile_IsMovedToTheValueFolderUnderTheNewID_AndASecondRunChangesNothing()
    {
        var image = OldImage(MetadataSource.AniDB, "/anime.jpg");
        var oldID = image.ID.ToString("N");
        var newID = image.NewID.ToString("N");
        var old = Write("AniDB", oldID, ".jpg", "poster");

        for (var run = 0; run < 2; run++)
        {
            var migrator = new ImageCacheMigrator(_root.FullName);
            migrator.RenameOldFolder("anidb", ["AniDB"]);

            Assert.Equal(run is 0 ? ImageMoveResult.Moved : ImageMoveResult.InPlace, MoveFile(migrator, image, ".jpg"));
            Assert.Equal("poster", File.ReadAllText(PathOf("anidb", newID, ".jpg")));
            Assert.False(File.Exists(PathOf("anidb", oldID, ".jpg")));
            Assert.False(File.Exists(old));
        }
    }

    [Fact]
    public void AFileAlreadyUnderTheNewID_IsLeftInPlaceWhileTheStoredIDIsOld()
    {
        var image = OldImage(MetadataSource.TMDB, "/b.jpg");
        var newID = image.NewID.ToString("N");
        Write("tmdb", newID, ".jpg", "moved");
        var migrator = new ImageCacheMigrator(_root.FullName);

        Assert.Equal(ImageMoveResult.InPlace, MoveFile(migrator, image, ".jpg"));
        Assert.Equal("moved", File.ReadAllText(PathOf("tmdb", newID, ".jpg")));
    }

    [Fact]
    public void AnImageWithoutAFile_IsReportedMissing()
        => Assert.Equal(ImageMoveResult.Missing, MoveFile(new ImageCacheMigrator(_root.FullName), OldImage(MetadataSource.User, "abc"), ".png"));

    #endregion
}
