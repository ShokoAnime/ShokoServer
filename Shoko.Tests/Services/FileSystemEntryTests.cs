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
}
