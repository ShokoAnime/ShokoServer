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

        var files = fileSystem.GetFileEntries(_root, recursive: true, cancellationToken: TestContext.Current.CancellationToken);
        var directories = fileSystem.GetDirectoryEntries(_root, recursive: true, cancellationToken: TestContext.Current.CancellationToken);

        var file = Assert.Single(files);
        Assert.Equal("video.mkv", file.Name);
        Assert.False(file.IsDirectory);
        Assert.Equal(5, file.FileStat?.Size);
        var directory = Assert.Single(directories);
        Assert.Equal("folder", directory.Name);
        Assert.True(directory.IsDirectory);
        Assert.NotNull(directory.DirectoryStat);
    }
}
