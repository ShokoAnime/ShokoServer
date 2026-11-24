using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.FileSystem;
using Shoko.Abstractions.FileSystem.Services;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.Tests.Services;

public sealed class FileSystemEntryTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("shoko-entry-").FullName;

    public void Dispose()
        => Directory.Delete(_root, true);

    [Fact]
    public void Stat_IsReadOnceAndOnlyWhenAskedFor()
    {
        var fileSystem = new Mock<IFileSystem>();
        var entry = new FileSystemEntry(fileSystem.Object, "/media/show/episode.mkv", isDirectory: false);

        Assert.Equal("episode.mkv", entry.Name);
        fileSystem.Verify(f => f.GetStat(It.IsAny<string?>(), It.IsAny<bool>()), Times.Never);

        _ = entry.Stat;
        _ = entry.FileStat;
        fileSystem.Verify(f => f.GetStat("/media/show/episode.mkv", true), Times.Once);
    }

    [Fact]
    public void AFailedRead_IsTriedAgain()
    {
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.SetupSequence(f => f.GetStat(It.IsAny<string?>(), It.IsAny<bool>()))
            .Throws(new IOException("Host is down"))
            .Returns((FileSystemStat?)null);
        var entry = new FileSystemEntry(fileSystem.Object, "/media/show/episode.mkv", isDirectory: false);

        Assert.Throws<IOException>(() => entry.Stat);
        Assert.Null(entry.Stat);
    }

    [Fact]
    public void IsDirectory_LeftOpen_IsReadOnceWithTheStat()
    {
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(f => f.GetStat(It.IsAny<string?>(), It.IsAny<bool>())).Returns(new DirectoryStat { Path = "/media/show-link", Identity = new("1", "2"), LinkCount = 1, LastWriteTime = DateTime.UnixEpoch, LastAccessTime = DateTime.UnixEpoch });
        var entry = new FileSystemEntry(fileSystem.Object, "/media/show-link", isDirectory: null, isSymbolicLink: true);

        fileSystem.Verify(f => f.GetStat(It.IsAny<string?>(), It.IsAny<bool>()), Times.Never);
        Assert.True(entry.IsDirectory);
        Assert.True(entry.IsDirectory);
        Assert.NotNull(entry.DirectoryStat);
        fileSystem.Verify(f => f.GetStat("/media/show-link", true), Times.Once);
    }

    [Fact]
    public void IsDirectory_LeftOpen_IsFalseWhenTheTargetCannotBeRead()
    {
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(f => f.GetStat(It.IsAny<string?>(), It.IsAny<bool>())).Throws(new UnauthorizedAccessException());
        var entry = new FileSystemEntry(fileSystem.Object, "/media/locked-link", isDirectory: null, isSymbolicLink: true);

        Assert.False(entry.IsDirectory);
    }

    [Fact]
    public void Listings_GiveEntriesOfTheirKind()
    {
        IFileSystem fileSystem = new FileSystemService(NullLogger<FileSystemService>.Instance);
        Directory.CreateDirectory(Path.Combine(_root, "folder"));
        File.WriteAllText(Path.Combine(_root, "folder", "video.mkv"), "video");

        var files = fileSystem.GetFileEntries(_root, new() { Recursive = true }, cancellationToken: TestContext.Current.CancellationToken);
        var directories = fileSystem.GetDirectoryEntries(_root, new() { Recursive = true }, cancellationToken: TestContext.Current.CancellationToken);

        var file = Assert.Single(files);
        Assert.Equal("video.mkv", file.Name);
        Assert.False(file.IsDirectory);
        Assert.Equal(5, file.FileStat?.Size);
        var directory = Assert.Single(directories);
        Assert.Equal("folder", directory.Name);
        Assert.True(directory.IsDirectory);
        Assert.NotNull(directory.DirectoryStat);
    }

    [Fact]
    public void Listings_MarkLinksAndWalkIntoThemOnlyWhenAsked()
    {
        IFileSystem fileSystem = new FileSystemService(NullLogger<FileSystemService>.Instance);
        var target = Directory.CreateDirectory(Path.Combine(_root, "target")).FullName;
        File.WriteAllText(Path.Combine(target, "video.mkv"), "video");
        var walk = Directory.CreateDirectory(Path.Combine(_root, "walk")).FullName;
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(walk, "link"), target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Assert.Skip("This user may not make symbolic links here.");
        }

        var all = fileSystem.GetFileSystemEntries(walk, new() { Recursive = true, FollowDirectoryLinks = true }, cancellationToken: TestContext.Current.CancellationToken);
        var notFollowed = fileSystem.GetFileSystemEntries(walk, new() { Recursive = true }, cancellationToken: TestContext.Current.CancellationToken);

        var link = Assert.Single(all, entry => entry.Name == "link");
        Assert.True(link.IsSymbolicLink);
        Assert.True(link.IsDirectory);
        Assert.Contains(all, entry => entry.Name == "video.mkv" && !entry.IsSymbolicLink);
        Assert.True(Assert.Single(notFollowed).IsSymbolicLink);
    }

    [Fact]
    public void Listings_LeaveOutFileAndDirectoryLinksApart()
    {
        IFileSystem fileSystem = new FileSystemService(NullLogger<FileSystemService>.Instance);
        var target = Directory.CreateDirectory(Path.Combine(_root, "target")).FullName;
        File.WriteAllText(Path.Combine(target, "video.mkv"), "video");
        var walk = Directory.CreateDirectory(Path.Combine(_root, "walk")).FullName;
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(walk, "folder-link"), target);
            File.CreateSymbolicLink(Path.Combine(walk, "file-link.mkv"), Path.Combine(target, "video.mkv"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Assert.Skip("This user may not make symbolic links here.");
        }

        string[] Names(FileSystemListingOptions options)
            => fileSystem.GetFileSystemEntries(walk, options, TestContext.Current.CancellationToken).Select(entry => entry.Name).Order().ToArray();

        Assert.Equal(["file-link.mkv", "folder-link"], Names(new()));
        // Read when asked, the listing having left it open.
        var kinds = fileSystem.GetFileSystemEntries(walk, cancellationToken: TestContext.Current.CancellationToken).ToDictionary(entry => entry.Name, entry => entry.IsDirectory);
        Assert.True(kinds["folder-link"]);
        Assert.False(kinds["file-link.mkv"]);
        Assert.Equal(["folder-link"], fileSystem.GetDirectoryEntries(walk, cancellationToken: TestContext.Current.CancellationToken).Select(entry => entry.Name).ToArray());
        Assert.Equal(["file-link.mkv"], fileSystem.GetFileEntries(walk, cancellationToken: TestContext.Current.CancellationToken).Select(entry => entry.Name).ToArray());
        Assert.Equal(["folder-link"], Names(new() { IncludeFileLinks = false }));
        Assert.Equal(["file-link.mkv"], Names(new() { IncludeDirectoryLinks = false }));
        // Walked into, though not listed itself.
        Assert.Equal(["file-link.mkv", "video.mkv"], Names(new() { Recursive = true, FollowDirectoryLinks = true, IncludeDirectoryLinks = false }));
    }

    [Fact]
    public void Listings_WalkALinkBackUpTheTreeOnce()
    {
        IFileSystem fileSystem = new FileSystemService(NullLogger<FileSystemService>.Instance);
        var show = Directory.CreateDirectory(Path.Combine(_root, "show")).FullName;
        File.WriteAllText(Path.Combine(show, "video.mkv"), "video");
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(show, "loop"), _root);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Assert.Skip("This user may not make symbolic links here.");
        }

        var files = fileSystem.GetFilePaths(_root, new() { Recursive = true, FollowDirectoryLinks = true }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(show, "video.mkv"), Assert.Single(files));
    }

    [Fact]
    public void OpenFile_TakesStreamOptions()
    {
        IFileSystem fileSystem = new FileSystemService(NullLogger<FileSystemService>.Instance);
        var path = Path.Combine(_root, "video.mkv");
        File.WriteAllText(path, "video");

        using (var stream = fileSystem.OpenFile(path, new FileStreamOptions { Share = FileShare.ReadWrite | FileShare.Delete, BufferSize = 1, Options = FileOptions.RandomAccess }))
        {
            // Shared for deletion, so the file can be moved while it is open.
            File.Move(path, path + ".moved");
            Assert.Equal(5, stream.Length);
        }

        Assert.True(File.Exists(path + ".moved"));
    }

    [Fact]
    public void AMissingDirectory_ListsNothing_OrThrowsWhenAsked()
    {
        IFileSystem fileSystem = new FileSystemService(NullLogger<FileSystemService>.Instance);
        var missing = Path.Combine(_root, "missing");

        Assert.Empty(fileSystem.GetFileSystemEntries(missing, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<DirectoryNotFoundException>(() => fileSystem.GetFileSystemEntries(missing, new() { ThrowOnError = true }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void AnUnreadableSubdirectory_IsSkipped_OrThrowsWhenAsked()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix permissions only.");
        if (OperatingSystem.IsWindows())
            return;
        IFileSystem fileSystem = new FileSystemService(NullLogger<FileSystemService>.Instance);
        var locked = Directory.CreateDirectory(Path.Combine(_root, "locked")).FullName;
        File.WriteAllText(Path.Combine(locked, "video.mkv"), "video");
        File.SetUnixFileMode(locked, UnixFileMode.None);
        try
        {
            Assert.SkipWhen(Directory.EnumerateFileSystemEntries(locked).Any(), "This user reads it anyway, as root does.");
        }
        catch (UnauthorizedAccessException)
        {
        }

        try
        {
            Assert.Equal(["locked"], fileSystem.GetFileSystemEntries(_root, new() { Recursive = true }, TestContext.Current.CancellationToken).Select(entry => entry.Name));
            Assert.Throws<UnauthorizedAccessException>(() => fileSystem.GetFileSystemEntries(_root, new() { Recursive = true, ThrowOnError = true }, TestContext.Current.CancellationToken));
        }
        finally
        {
            File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public void ALinkToAMissingTarget_IsMadeWhenTheKindIsGiven_AndReadAsStored()
    {
        IFileSystem fileSystem = new FileSystemService(NullLogger<FileSystemService>.Instance);
        var link = Path.Combine(_root, "link");
        try
        {
            fileSystem.CreateSymbolicLink("not-there-yet", link, isDirectory: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Assert.Skip("This user may not make symbolic links here.");
        }

        Assert.Equal("not-there-yet", fileSystem.ReadSymbolicLink(link));
        Assert.Null(fileSystem.ReadSymbolicLink(Path.Combine(_root, "missing")));
        Assert.Throws<FileNotFoundException>(() => fileSystem.CreateSymbolicLink("also-not-there", Path.Combine(_root, "other")));

        var entry = Assert.Single(fileSystem.GetFileSystemEntries(_root, cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(entry.IsSymbolicLink);
        Assert.False(entry.IsDirectory);
    }
}
