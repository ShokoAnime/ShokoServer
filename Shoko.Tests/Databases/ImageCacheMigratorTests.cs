using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases;
using Xunit;

namespace Shoko.Tests.Databases;

/// <summary>
/// Unit tests for <see cref="ImageCacheMigrator"/>, which moves cached image
/// files from the old enum-named folders to the source value folders.
/// </summary>
public sealed class ImageCacheMigratorTests : IDisposable
{
    private const string Id = "0123456789abcdef0123456789abcdef";

    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("shoko-image-cache-");

    public void Dispose()
        => _root.Delete(recursive: true);

    #region Helpers

    private string Write(string folder, string fileName, string content = "image")
    {
        var path = Path.Join(_root.FullName, folder, fileName[..2], fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private string PathOf(string folder, string fileName)
        => Path.Join(_root.FullName, folder, fileName[..2], fileName);

    private IReadOnlyList<string> FolderNames()
        => _root.EnumerateDirectories().Select(directory => directory.Name).Order(StringComparer.Ordinal).ToList();

    private static ImageMoveResult Normalize(ImageCacheMigrator migrator, MetadataSource source, string id, string extension)
    {
        var oldNames = ImageCacheMigrator.GetOldFolderNames(source);
        var files = migrator.FindFiles(source.Value, oldNames, id);
        return migrator.MoveToTarget(migrator.GetTargetPath(source.Value, id, extension), files);
    }

    // The test needs "TMDB" and "tmdb" to be two folders.
    private void SkipOnCaseInsensitiveFileSystem()
    {
        var probe = Path.Join(_root.FullName, "case-probe");
        File.WriteAllBytes(probe, []);
        var caseInsensitive = File.Exists(Path.Join(_root.FullName, "CASE-PROBE"));
        File.Delete(probe);
        Assert.SkipWhen(caseInsensitive, "The file system ignores case.");
    }

    #endregion

    #region Old Folder Names

    [Theory]
    [InlineData("anilist", "AniList")]
    [InlineData("generated", "LocallyGenerated")]
    [InlineData("image-cache-test-source", null)]
    public void GetOldFolderNames_GivesTheOldEnumSpelling_OrNothingForASourceTheOldEnumDidNotHave(string value, string? oldName)
        => Assert.Equal(oldName is null ? Array.Empty<string>() : [oldName], ImageCacheMigrator.GetOldFolderNames(MetadataSource.Parse(value)));

    #endregion

    #region Folders

    [Fact]
    public void RenameOldFolder_RenamesACaseOnlyDifference()
    {
        var file = Id + ".jpg";
        Write("AniDB", file);
        var migrator = new ImageCacheMigrator(_root.FullName);

        Assert.True(migrator.RenameOldFolder("anidb", ["AniDB"]));

        Assert.Equal(["anidb"], FolderNames());
        Assert.True(File.Exists(PathOf("anidb", file)));
        Assert.False(migrator.RenameOldFolder("anidb", ["AniDB"]));
    }

    [Fact]
    public void RenameOldFolder_LeavesTheOldFolderWhenTheValueFolderExists()
    {
        SkipOnCaseInsensitiveFileSystem();
        Write("TMDB", Id + ".jpg");
        Directory.CreateDirectory(Path.Join(_root.FullName, "tmdb"));
        var migrator = new ImageCacheMigrator(_root.FullName);

        Assert.False(migrator.RenameOldFolder("tmdb", ["TMDB"]));

        Assert.Equal(["TMDB", "tmdb"], FolderNames());
    }

    [Fact]
    public void RenameOldFolder_FinishesAnInterruptedCaseOnlyRename()
    {
        var file = Id + ".jpg";
        Write("anidb.moving", file);
        var migrator = new ImageCacheMigrator(_root.FullName);

        Assert.True(migrator.RenameOldFolder("anidb", ["AniDB"]));

        Assert.Equal(["anidb"], FolderNames());
        Assert.True(File.Exists(PathOf("anidb", file)));
    }

    [Fact]
    public void AnInterruptedRenameNextToAValueFolder_IsMovedImageByImageAndRemoved()
    {
        Directory.CreateDirectory(Path.Join(_root.FullName, "user"));
        Write("user.moving", Id + ".png", "uploaded");
        var migrator = new ImageCacheMigrator(_root.FullName);

        Assert.False(migrator.RenameOldFolder("user", ["User"]));
        Assert.Equal(ImageMoveResult.Moved, Normalize(migrator, MetadataSource.User, Id, ".png"));
        Assert.Equal(1, migrator.RemoveEmptyOldFolders(["User"], new HashSet<string>(["user"])));

        Assert.Equal(["user"], FolderNames());
        Assert.Equal("uploaded", File.ReadAllText(PathOf("user", Id + ".png")));
    }

    [Fact]
    public void RemoveEmptyOldFolders_RemovesOnlyEmptyOldFolders()
    {
        Directory.CreateDirectory(Path.Join(_root.FullName, "AniDB", "01"));
        Write("TMDB", Id + ".jpg");
        Directory.CreateDirectory(Path.Join(_root.FullName, "user"));
        var migrator = new ImageCacheMigrator(_root.FullName);

        var removed = migrator.RemoveEmptyOldFolders(["AniDB", "TMDB", "user"], new HashSet<string>(["anidb", "tmdb", "user"]));

        Assert.Equal(1, removed);
        Assert.Equal(["TMDB", "user"], FolderNames());
    }

    #endregion

    #region Files

    [Theory]
    [InlineData(".jpg")]
    [InlineData("")]
    public void FindFiles_FindsAFileInTheOldFolderWithOrWithoutItsExtension(string extension)
    {
        var path = Write("AniDB", Id + extension);
        var migrator = new ImageCacheMigrator(_root.FullName);

        Assert.Equal([path], migrator.FindFiles("anidb", ["AniDB"], Id));
    }

    [Fact]
    public void FindFiles_ListsTheValueFolderFirst()
    {
        SkipOnCaseInsensitiveFileSystem();
        var old = Write("TMDB", Id + ".jpg");
        var current = Write("tmdb", Id + ".png");
        var migrator = new ImageCacheMigrator(_root.FullName);

        Assert.Equal([current, old], migrator.FindFiles("tmdb", ["TMDB"], Id));
    }

    [Fact]
    public void FindFiles_IgnoresOtherImages()
    {
        Write("AniDB", "01" + new string('f', 30) + ".jpg");
        var migrator = new ImageCacheMigrator(_root.FullName);

        Assert.Empty(migrator.FindFiles("anidb", ["AniDB"], Id));
    }

    [Fact]
    public void MoveToTarget_MovesAFileFromTheOldFolder()
    {
        SkipOnCaseInsensitiveFileSystem();
        Write("AniList", Id + ".jpg", "old");
        var migrator = new ImageCacheMigrator(_root.FullName);

        Assert.Equal(ImageMoveResult.Moved, Normalize(migrator, MetadataSource.Parse("anilist"), Id, ".jpg"));

        Assert.Equal("old", File.ReadAllText(PathOf("anilist", Id + ".jpg")));
        Assert.False(File.Exists(PathOf("AniList", Id + ".jpg")));
    }

    [Fact]
    public void MoveToTarget_PrefersAFileWithTheTargetExtension()
    {
        SkipOnCaseInsensitiveFileSystem();
        Write("User", Id, "bare");
        Write("User", Id + ".png", "png");
        var migrator = new ImageCacheMigrator(_root.FullName);

        Assert.Equal(ImageMoveResult.Moved, Normalize(migrator, MetadataSource.User, Id, ".png"));

        Assert.Equal("png", File.ReadAllText(PathOf("user", Id + ".png")));
        Assert.False(File.Exists(PathOf("User", Id)));
    }

    [Fact]
    public void MoveToTarget_NeverOverwritesTheTargetAndDeletesTheDuplicate()
    {
        SkipOnCaseInsensitiveFileSystem();
        Write("tmdb", Id + ".jpg", "new");
        Write("TMDB", Id + ".jpg", "old");
        var migrator = new ImageCacheMigrator(_root.FullName);

        Assert.Equal(ImageMoveResult.InPlace, Normalize(migrator, MetadataSource.TMDB, Id, ".jpg"));

        Assert.Equal("new", File.ReadAllText(PathOf("tmdb", Id + ".jpg")));
        Assert.False(File.Exists(PathOf("TMDB", Id + ".jpg")));
    }

    [Fact]
    public void MoveToTarget_ReportsAMissingFile()
    {
        var migrator = new ImageCacheMigrator(_root.FullName);

        Assert.Equal(ImageMoveResult.Missing, Normalize(migrator, MetadataSource.AniDB, Id, ".jpg"));
    }

    [Fact]
    public void MoveToTarget_OnACaseInsensitiveFileSystem_NeverTouchesAFileThatDiffersFromTheTargetOnlyByCase()
    {
        var file = Write("AniDB", Id + ".jpg");
        var migrator = new ImageCacheMigrator(_root.FullName, caseInsensitive: true);

        migrator.MoveToTarget(migrator.GetTargetPath("anidb", Id, ".jpg"), [file]);

        Assert.True(File.Exists(file));
    }

    [Fact]
    public void MoveToTarget_FindsAFileItMovedEarlier_InAFolderItHadAlreadyRead()
    {
        SkipOnCaseInsensitiveFileSystem();
        Write("TMDB", Id + ".jpg", "old");
        Write("tmdb", Id[..2] + new string('f', 30) + ".jpg", "other");
        var migrator = new ImageCacheMigrator(_root.FullName);
        var files = migrator.FindFiles("tmdb", ["TMDB"], Id);

        Assert.Equal(ImageMoveResult.Moved, migrator.MoveToTarget(migrator.GetTargetPath("tmdb", Id, ".jpg"), files));
        Assert.Equal(ImageMoveResult.InPlace, Normalize(migrator, MetadataSource.TMDB, Id, ".jpg"));

        Assert.True(migrator.FileExists(PathOf("tmdb", Id + ".jpg")));
        Assert.False(migrator.FileExists(PathOf("TMDB", Id + ".jpg")));
        Assert.Equal("old", File.ReadAllText(PathOf("tmdb", Id + ".jpg")));
    }

    [Fact]
    public void MoveToTarget_MovesADuplicateInstead_WhenTheTargetIsGoneFromDisk()
    {
        SkipOnCaseInsensitiveFileSystem();
        Write("tmdb", Id + ".jpg", "new");
        Write("TMDB", Id + ".jpg", "old");
        var migrator = new ImageCacheMigrator(_root.FullName);
        var files = migrator.FindFiles("tmdb", ["TMDB"], Id);
        File.Delete(PathOf("tmdb", Id + ".jpg"));

        Assert.Equal(ImageMoveResult.Moved, migrator.MoveToTarget(migrator.GetTargetPath("tmdb", Id, ".jpg"), files));

        Assert.Equal("old", File.ReadAllText(PathOf("tmdb", Id + ".jpg")));
        Assert.False(File.Exists(PathOf("TMDB", Id + ".jpg")));
    }

    [Fact]
    public void FileExists_ReadsTheFolder_AndIgnoresOtherNames()
    {
        var file = Write("anidb", Id + ".jpg");
        var migrator = new ImageCacheMigrator(_root.FullName);

        Assert.True(migrator.FileExists(file));
        Assert.False(migrator.FileExists(PathOf("anidb", Id + ".png")));
        Assert.False(migrator.FileExists(PathOf("tmdb", Id + ".jpg")));
    }

    #endregion

    #region Whole Cache

    [Fact]
    public void AWholeDailyBuildCache_EndsUpInTheValueFolders_AndASecondRunChangesNothing()
    {
        var anidb = "aa" + new string('1', 30);
        var tmdb = "bb" + new string('2', 30);
        var generated = "cc" + new string('3', 30);
        Write("AniDB", anidb + ".jpg");
        Write("TMDB", tmdb);
        Write("LocallyGenerated", generated + ".png", "generated");
        // A re-download in another format left the older file behind.
        Write("LocallyGenerated", generated + ".jpg", "stale");
        var images = new (MetadataSource Source, string Id, string Extension)[]
        {
            (MetadataSource.AniDB, anidb, ".jpg"),
            (MetadataSource.TMDB, tmdb, ".jpg"),
            (MetadataSource.Generated, generated, ".png"),
        };

        for (var run = 0; run < 2; run++)
        {
            var migrator = new ImageCacheMigrator(_root.FullName);
            var sources = images.Select(image => image.Source).Distinct().ToList();
            foreach (var source in sources)
                migrator.RenameOldFolder(source.Value, ImageCacheMigrator.GetOldFolderNames(source));
            var results = images.Select(image => Normalize(migrator, image.Source, image.Id, image.Extension)).ToList();
            migrator.RemoveEmptyOldFolders(sources.SelectMany(ImageCacheMigrator.GetOldFolderNames), sources.Select(source => source.Value).ToHashSet());

            if (run is 0)
                Assert.Equal([ImageMoveResult.InPlace, ImageMoveResult.Moved, ImageMoveResult.InPlace], results);
            else
                Assert.All(results, result => Assert.Equal(ImageMoveResult.InPlace, result));
            Assert.Equal(["anidb", "generated", "tmdb"], FolderNames());
            Assert.True(File.Exists(PathOf("anidb", anidb + ".jpg")));
            Assert.True(File.Exists(PathOf("tmdb", tmdb + ".jpg")));
            Assert.Equal("generated", File.ReadAllText(PathOf("generated", generated + ".png")));
            Assert.False(File.Exists(PathOf("generated", generated + ".jpg")));
        }
    }

    #endregion
}
