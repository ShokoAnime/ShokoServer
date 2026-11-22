using System;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.FileSystem;
using Shoko.Abstractions.FileSystem.Services;
using Shoko.Abstractions.Video;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.Tests.Services;

public sealed class ManagedFolderPresenceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("shoko-presence-").FullName;

    public void Dispose()
        => Directory.Delete(_root, true);

    private static IManagedFolder Folder(string path)
        => Mock.Of<IManagedFolder>(folder => folder.ID == 1 && folder.Path == path);

    private static DirectoryStat DirectoryOn(string path, string volume)
        => new()
        {
            Path = path,
            Identity = new(volume, "1"),
            LinkCount = 1,
            LastWriteTime = DateTime.UnixEpoch,
            LastAccessTime = DateTime.UnixEpoch,
        };

    [Fact]
    public void IsPresent_WhenTheRootHasEntries()
    {
        File.WriteAllText(Path.Combine(_root, "video.mkv"), "video");
        var presence = new ManagedFolderPresence(new FileSystemService(NullLogger<FileSystemService>.Instance));

        Assert.True(presence.IsPresent(Folder(_root)));
        Assert.True(presence.IsPresent(Folder(_root)));
    }

    [Fact]
    public void IsNotPresent_WhenTheRootIsEmptyOrMissing()
    {
        var fileSystem = new FileSystemService(NullLogger<FileSystemService>.Instance);

        Assert.False(new ManagedFolderPresence(fileSystem).IsPresent(Folder(_root)));
        Assert.False(new ManagedFolderPresence(fileSystem).IsPresent(Folder(Path.Combine(_root, "missing"))));
    }

    [Fact]
    public void IsNotPresent_OnceTheRootMovedToAnotherVolume()
    {
        File.WriteAllText(Path.Combine(_root, "video.mkv"), "video");
        var fileSystem = new Mock<IFileSystem> { CallBase = true };
        fileSystem.SetupSequence(f => f.GetStat(_root, true))
            .Returns(DirectoryOn(_root, "a"))
            .Returns(DirectoryOn(_root, "a"))
            .Returns(DirectoryOn(_root, "b"));
        var presence = new ManagedFolderPresence(fileSystem.Object);

        Assert.True(presence.IsPresent(Folder(_root)));
        Assert.True(presence.IsPresent(Folder(_root)));
        Assert.False(presence.IsPresent(Folder(_root)));
    }

    [Fact]
    public void IsNotPresent_WhenTheRootCannotBeRead()
    {
        File.WriteAllText(Path.Combine(_root, "video.mkv"), "video");
        var fileSystem = new Mock<IFileSystem> { CallBase = true };
        fileSystem.SetupSequence(f => f.GetStat(_root, true))
            .Returns(DirectoryOn(_root, "a"))
            .Throws(new IOException("Host is down"));
        var presence = new ManagedFolderPresence(fileSystem.Object);

        Assert.True(presence.IsPresent(Folder(_root)));
        Assert.False(presence.IsPresent(Folder(_root)));
    }
}
