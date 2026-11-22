using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging.Abstractions;
using Shoko.Abstractions.FileSystem;
using Shoko.Abstractions.FileSystem.Services;
using Shoko.Abstractions.Utilities;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.Tests.Services;

public sealed class FileSystemServiceTests : IDisposable
{
    private readonly FileSystemService _fileSystem = new(NullLogger<FileSystemService>.Instance);

    private readonly string _root = Directory.CreateTempSubdirectory("shoko-fs-").FullName;

    public void Dispose()
        => Directory.Delete(_root, true);

    private StatIdentity? Identity(string? path)
        => _fileSystem.GetStat(path)?.Identity;

    /// <summary>
    ///   Whether symbolic links can be made in the test's folder: always on
    ///   Unix, on Windows for an administrator or with developer mode on.
    /// </summary>
    private bool CanLink
        => _fileSystem.CanCreateSymbolicLinks(_root);

    private string MakeFile(string name, string contents = "video")
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, contents);
        return path;
    }

    [Fact]
    public void CopyFile_MakesAnotherFileWithTheSameContents()
    {
        var source = MakeFile("source.mkv");
        var copy = Path.Combine(_root, "copy.mkv");

        _fileSystem.CopyFile(source, copy);

        Assert.Equal("video", File.ReadAllText(copy));
        Assert.NotNull(Identity(copy));
        Assert.NotEqual(Identity(source), Identity(copy));
        Assert.Throws<IOException>(() => _fileSystem.CopyFile(source, copy));
    }

    [Fact]
    public void CreateHardLink_SharesTheFilesIdentity()
    {
        var source = MakeFile("source.mkv");
        var link = Path.Combine(_root, "link.mkv");

        _fileSystem.CreateHardLink(source, link);

        Assert.NotNull(Identity(source));
        Assert.Equal(Identity(source), Identity(link));
        Assert.ThrowsAny<IOException>(() => _fileSystem.CreateHardLink(source, link));
    }

    [Fact]
    public void CreateHardLink_RefusesADirectoryByName()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, "folder")).FullName;

        var refusal = Assert.ThrowsAny<IOException>(() => _fileSystem.CreateHardLink(folder, Path.Combine(_root, "link")));

        Assert.Contains("directory", refusal.Message);
    }

    [Fact]
    public void CreateHardLink_ThrowsForAnotherVolume()
    {
        Assert.SkipUnless(PlatformUtility.IsUnixLike && Directory.Exists("/dev/shm"), "Needs a second volume.");
        var source = MakeFile("source.mkv");
        var probe = Path.Combine("/dev/shm", $"shoko-fs-{Guid.NewGuid():N}");
        File.WriteAllText(probe, "probe");
        var sameVolume = Identity(probe)?.VolumeID == Identity(source)?.VolumeID;
        File.Delete(probe);
        Assert.SkipWhen(sameVolume, "The temporary folder is on /dev/shm's volume.");

        Assert.ThrowsAny<IOException>(() => _fileSystem.CreateHardLink(source, Path.Combine("/dev/shm", $"shoko-fs-{Guid.NewGuid():N}.mkv")));
    }

    [Fact]
    public void CreateSymbolicLink_AnswersForTheFileItPointsTo()
    {
        Assert.SkipUnless(CanLink, "This user may not make symbolic links here.");
        var source = MakeFile("source.mkv");
        var link = Path.Combine(_root, "link.mkv");

        _fileSystem.CreateSymbolicLink(source, link);

        Assert.Equal(source, File.ResolveLinkTarget(link, false)?.FullName);
        Assert.Equal(Identity(source), Identity(link));
    }

    [Fact]
    public void CreateSymbolicLink_LinksADirectoryOrARelativeTarget()
    {
        Assert.SkipUnless(CanLink, "This user may not make symbolic links here.");
        var folder = Directory.CreateDirectory(Path.Combine(_root, "folder")).FullName;
        MakeFile("source.mkv");
        var folderLink = Path.Combine(_root, "folder-link");
        var relativeLink = Path.Combine(folder, "relative.mkv");

        _fileSystem.CreateSymbolicLink(folder, folderLink);
        _fileSystem.CreateSymbolicLink(Path.Combine("..", "source.mkv"), relativeLink);

        Assert.IsType<DirectoryStat>(_fileSystem.GetStat(folderLink));
        Assert.Equal(Identity(Path.Combine(_root, "source.mkv")), Identity(relativeLink));
        Assert.Equal(Path.Combine("..", "source.mkv"), File.ResolveLinkTarget(relativeLink, false) is { } target ? Path.GetRelativePath(folder, target.FullName) : null);
    }

    [Fact]
    public void CreateSymbolicLink_PointsToTheNextStep()
    {
        Assert.SkipUnless(CanLink, "This user may not make symbolic links here.");
        var dangling = Path.Combine(_root, "dangling.mkv");
        var link = Path.Combine(_root, "link.mkv");
        File.CreateSymbolicLink(dangling, Path.Combine(_root, "missing.mkv"));

        _fileSystem.CreateSymbolicLink(dangling, link);

        Assert.Equal(dangling, _fileSystem.ResolveSymbolicLink(link, returnFinalTarget: false));
        // A file link: Windows says so, Unix links have no kind.
        Assert.NotEqual(true, Assert.IsType<SymbolicLinkStat>(_fileSystem.GetStat(link, followSymbolicLinks: false)).IsDirectoryLink);
    }

    [Fact]
    public void CreateSymbolicLink_ThrowsForAMissingSource()
        => Assert.Throws<FileNotFoundException>(() => _fileSystem.CreateSymbolicLink(Path.Combine(_root, "missing.mkv"), Path.Combine(_root, "link.mkv")));

    [Fact]
    public void GetStat_IdentityIsLowercaseHexAndNullForAMissingFile()
    {
        var identity = Identity(MakeFile("source.mkv"));

        Assert.NotNull(identity);
        Assert.All(new[] { identity.VolumeID, identity.UniqueID }, value => Assert.Matches("^(0|[1-9a-f][0-9a-f]*)$", value));
        Assert.Null(Identity(Path.Combine(_root, "missing.mkv")));
        Assert.Null(Identity(null));
    }

    [Fact]
    public void GetStat_ReadsAFileWithItsSizeAndLinks()
    {
        var source = MakeFile("source.mkv");
        _fileSystem.CreateHardLink(source, Path.Combine(_root, "link.mkv"));

        var info = Assert.IsType<FileStat>(_fileSystem.GetStat(source));

        Assert.Equal(source, info.Path);
        Assert.Equal(5, info.Size);
        Assert.Equal(2, info.LinkCount);
        Assert.Equal(File.GetLastWriteTimeUtc(source), info.LastWriteTime);
        Assert.Equal(Identity(source), info.Identity);
    }

    [Fact]
    public void GetStat_ReadsTheSameWithoutStatx()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "statx is Linux only.");
        var source = MakeFile("source.mkv");
        var withStatx = Assert.IsType<FileStat>(_fileSystem.GetStat(source));

        FileSystemService._statxUnavailable = true;
        FileStat withStat;
        try
        {
            withStat = Assert.IsType<FileStat>(_fileSystem.GetStat(source));
        }
        finally
        {
            FileSystemService._statxUnavailable = false;
        }

        Assert.Equal(withStatx.Identity, withStat.Identity);
        Assert.Equal(withStatx.Size, withStat.Size);
        Assert.Equal(withStatx.LastWriteTime, withStat.LastWriteTime);
        Assert.Equal(withStatx.ChangeTime, withStat.ChangeTime);
        Assert.Null(withStat.CreationTime);
    }

    [Fact]
    public void GetFileAndDirectoryStat_GiveOnlyTheirKind()
    {
        var file = MakeFile("source.mkv");
        var folder = Directory.CreateDirectory(Path.Combine(_root, "folder")).FullName;
        IFileSystem fileSystem = _fileSystem;

        Assert.NotNull(fileSystem.GetFileStat(file));
        Assert.Null(fileSystem.GetFileStat(folder));
        Assert.NotNull(fileSystem.GetDirectoryStat(folder));
        Assert.Null(fileSystem.GetDirectoryStat(file));
    }

    [Fact]
    public void GetStat_ReadsSpecialFiles()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Needs /dev and mkfifo.");
        var pipe = Path.Combine(_root, "pipe");
        Assert.Equal(0, Mono.Unix.Native.Syscall.mkfifo(pipe, Mono.Unix.Native.FilePermissions.DEFFILEMODE));

        var pipeStat = Assert.IsType<SpecialFileStat>(_fileSystem.GetStat(pipe));
        var deviceStat = Assert.IsType<SpecialFileStat>(_fileSystem.GetStat("/dev/null"));

        Assert.Equal(SpecialFileKind.Pipe, pipeStat.Kind);
        Assert.Null(pipeStat.DeviceID);
        Assert.Equal(SpecialFileKind.CharacterDevice, deviceStat.Kind);
        Assert.Equal("103", deviceStat.DeviceID);
    }

    [Fact]
    public void GetVolumeStat_ReadsTheVolumeAnEntryIsOn()
    {
        var file = MakeFile("source.mkv");

        var volume = _fileSystem.GetVolumeStat(file);

        Assert.NotNull(volume);
        Assert.Equal(Identity(file)!.VolumeID, volume.VolumeID);
        Assert.StartsWith(volume.MountPoint.TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(file));
        Assert.True(volume.TotalSize > 0 && volume.FreeSpace >= volume.AvailableFreeSpace && volume.AvailableFreeSpace >= 0);
        Assert.Null(_fileSystem.GetVolumeStat(Path.Combine(_root, "missing.mkv")));
    }

    [Fact]
    public void GetVolumeMounts_LeavesOutTheSystemsOwnUnlessAsked()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Needs /proc.");

        var mounts = _fileSystem.GetVolumeMounts();
        var all = _fileSystem.GetVolumeMounts(includeSystem: true);

        Assert.DoesNotContain(mounts, mount => mount.IsSystem);
        Assert.Contains(mounts, mount => mount.MountPoint == "/");
        Assert.Contains(all, mount => mount is { MountPoint: "/proc", IsSystem: true });
        Assert.All(mounts, mount => Assert.IsNotType<VolumeStat>(mount));
    }

    [Fact]
    public void GetVolumeMounts_ReadsTheVolumesWhenAsked()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Needs /proc.");

        var root = Assert.Single(_fileSystem.GetVolumeMounts(includeStats: true), mount => mount.MountPoint == "/");

        var stat = Assert.IsType<VolumeStat>(root);
        Assert.Equal(Identity("/")!.VolumeID, stat.VolumeID);
        Assert.True(stat.TotalSize > 0);
    }

    [Fact]
    public void GetVolumeMount_FindsTheMountOfAPathAndFollowsALinkAtItsEnd()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux() && Directory.Exists("/dev/shm"), "Needs /proc and /dev/shm.");
        var link = Path.Combine(_root, "shared");
        File.CreateSymbolicLink(link, "/dev/shm");

        var own = _fileSystem.GetVolumeMount(MakeFile("source.mkv"));
        var linked = _fileSystem.GetVolumeMount(link + Path.DirectorySeparatorChar);

        Assert.NotNull(own);
        Assert.StartsWith(own.MountPoint.TrimEnd('/'), _root);
        Assert.NotNull(linked);
        Assert.Equal("/dev/shm", linked.MountPoint);
        Assert.Equal(VolumeKind.Memory, linked.Kind);
    }

    [Fact]
    public void GetVolumeStat_TellsAMemoryVolume()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux() && Directory.Exists("/dev/shm"), "Needs /dev/shm.");

        var volume = _fileSystem.GetVolumeStat("/dev/shm");

        Assert.NotNull(volume);
        Assert.Equal("/dev/shm", volume.MountPoint);
        Assert.Equal("tmpfs", volume.FormatName);
        Assert.Equal(VolumeKind.Memory, volume.Kind);
    }

    [Fact]
    public void GetStat_ReadsADirectory()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, "folder")).FullName;

        var info = Assert.IsType<DirectoryStat>(_fileSystem.GetStat(folder));

        Assert.Equal(Directory.GetLastWriteTimeUtc(folder), info.LastWriteTime);
        Assert.NotEqual(Identity(_root), info.Identity);
    }

    [Fact]
    public void GetStat_ReadsTheLinkItselfWhenNotFollowing()
    {
        Assert.SkipUnless(CanLink, "This user may not make symbolic links here.");
        var source = MakeFile("source.mkv");
        var link = Path.Combine(_root, "link.mkv");
        var dangling = Path.Combine(_root, "dangling.mkv");
        _fileSystem.CreateSymbolicLink(source, link);
        File.CreateSymbolicLink(dangling, Path.Combine(_root, "missing.mkv"));

        var linkInfo = Assert.IsType<SymbolicLinkStat>(_fileSystem.GetStat(link, followSymbolicLinks: false));

        Assert.Equal(source, linkInfo.Target);
        Assert.NotEqual(Identity(source), linkInfo.Identity);
        Assert.IsType<FileStat>(_fileSystem.GetStat(link));
        Assert.Null(_fileSystem.GetStat(dangling));
        Assert.IsType<SymbolicLinkStat>(_fileSystem.GetStat(dangling, followSymbolicLinks: false));
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void GetStat_ThrowsWhenItMayNotLook()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows() || Environment.UserName is "root", "Needs Unix permissions that apply.");
        var folder = Directory.CreateDirectory(Path.Combine(_root, "locked")).FullName;
        var file = Path.Combine(folder, "file.mkv");
        File.WriteAllText(file, "video");
        File.SetUnixFileMode(folder, UnixFileMode.None);
        try
        {
            Assert.Throws<UnauthorizedAccessException>(() => _fileSystem.GetStat(file));
        }
        finally
        {
            File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public void GetFileSystemInfo_GivesWhicheverIsThere()
    {
        var file = MakeFile("source.mkv");
        var folder = Directory.CreateDirectory(Path.Combine(_root, "folder")).FullName;

        Assert.IsType<FileInfo>(_fileSystem.GetFileSystemInfo(file));
        Assert.IsType<DirectoryInfo>(_fileSystem.GetFileSystemInfo(folder));
        Assert.Null(_fileSystem.GetFileSystemInfo(Path.Combine(_root, "missing")));
        Assert.Null(_fileSystem.GetDirectoryInfo(file));
        Assert.Null(_fileSystem.GetFileInfo(folder));
    }

    [Fact]
    public void ResolveSymbolicLink_FollowsChainsAndDanglingLinks()
    {
        Assert.SkipUnless(CanLink, "This user may not make symbolic links here.");
        var source = MakeFile("source.mkv");
        var first = Path.Combine(_root, "first.mkv");
        var second = Path.Combine(_root, "second.mkv");
        var dangling = Path.Combine(_root, "dangling.mkv");
        _fileSystem.CreateSymbolicLink(source, first);
        _fileSystem.CreateSymbolicLink(first, second);
        File.CreateSymbolicLink(dangling, Path.Combine(_root, "missing.mkv"));

        Assert.Equal(source, _fileSystem.ResolveSymbolicLink(second));
        Assert.Equal(Path.Combine(_root, "missing.mkv"), _fileSystem.ResolveSymbolicLink(dangling));
        Assert.Null(_fileSystem.ResolveSymbolicLink(source));
        Assert.Null(_fileSystem.ResolveSymbolicLink(Path.Combine(_root, "nothing.mkv")));
    }

    [Fact]
    public void ResolveSymbolicLink_StopsAtTheFirstTargetWhenAsked()
    {
        Assert.SkipUnless(CanLink, "This user may not make symbolic links here.");
        var source = MakeFile("source.mkv");
        var first = Path.Combine(_root, "first.mkv");
        var second = Path.Combine(_root, "second.mkv");
        _fileSystem.CreateSymbolicLink(source, first);
        _fileSystem.CreateSymbolicLink("first.mkv", second);

        Assert.Equal(first, _fileSystem.ResolveSymbolicLink(second, returnFinalTarget: false));
        Assert.Equal(source, _fileSystem.ResolveSymbolicLink(second));
    }

    [Fact]
    public void ResolveSymbolicLink_ThrowsForALoop()
    {
        Assert.SkipUnless(CanLink, "This user may not make symbolic links here.");
        var a = Path.Combine(_root, "a.mkv");
        var b = Path.Combine(_root, "b.mkv");
        var self = Path.Combine(_root, "self.mkv");
        File.CreateSymbolicLink(a, b);
        File.CreateSymbolicLink(b, a);
        File.CreateSymbolicLink(self, self);

        var loop = Assert.Throws<SymbolicLinkLoopException>(() => _fileSystem.ResolveSymbolicLink(a));
        Assert.Equal(a, loop.Path);
        Assert.Equal(a, loop.LoopPath);
        Assert.Throws<SymbolicLinkLoopException>(() => _fileSystem.ResolveSymbolicLink(self));
        Assert.Equal(b, _fileSystem.ResolveSymbolicLink(a, returnFinalTarget: false));
    }

    [Fact]
    public void ResolveSymbolicLink_ThrowsForAChainOfMoreThanFortyLinks()
    {
        Assert.SkipUnless(CanLink, "This user may not make symbolic links here.");
        var previous = MakeFile("source.mkv");
        for (var i = 1; i <= 41; i++)
        {
            var link = Path.Combine(_root, $"link-{i}.mkv");
            File.CreateSymbolicLink(link, previous);
            previous = link;
        }

        Assert.Equal(Path.Combine(_root, "source.mkv"), _fileSystem.ResolveSymbolicLink(Path.Combine(_root, "link-40.mkv")));
        Assert.Throws<SymbolicLinkLoopException>(() => _fileSystem.ResolveSymbolicLink(Path.Combine(_root, "link-41.mkv")));
    }

    [Fact]
    public void IsDirectoryEmpty_OnlyForAnExistingEmptyDirectory()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, "folder")).FullName;

        Assert.True(_fileSystem.IsDirectoryEmpty(folder));
        MakeFile(Path.Combine("folder", "video.mkv"));
        Assert.False(_fileSystem.IsDirectoryEmpty(folder));
        Assert.False(_fileSystem.IsDirectoryEmpty(Path.Combine(_root, "missing")));
    }

    [Fact]
    public void OpenFile_OpensWithTheGivenModes()
    {
        var path = Path.Combine(_root, "new.mkv");

        using (var stream = _fileSystem.OpenFile(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            stream.Write("video"u8);

        using var read = _fileSystem.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        Assert.Equal(5, read.Length);
        Assert.Throws<IOException>(() => _fileSystem.OpenFile(path, FileMode.CreateNew, FileAccess.Write, FileShare.None));
    }

    [Fact]
    public void GetFileSystemInfo_FindsLinksByTheirTarget()
    {
        Assert.SkipUnless(CanLink, "This user may not make symbolic links here.");
        var folder = Directory.CreateDirectory(Path.Combine(_root, "folder")).FullName;
        var folderLink = Path.Combine(_root, "folder-link");
        var linkToFolderLink = Path.Combine(_root, "folder-link-link");
        var dangling = Path.Combine(_root, "dangling.mkv");
        _fileSystem.CreateSymbolicLink(folder, folderLink);
        _fileSystem.CreateSymbolicLink(folderLink, linkToFolderLink);
        File.CreateSymbolicLink(dangling, Path.Combine(_root, "missing.mkv"));

        Assert.IsType<DirectoryInfo>(_fileSystem.GetFileSystemInfo(folderLink));
        Assert.IsType<DirectoryInfo>(_fileSystem.GetFileSystemInfo(linkToFolderLink));
        var danglingInfo = Assert.IsType<FileInfo>(_fileSystem.GetFileSystemInfo(dangling));
        Assert.Equal(Path.Combine(_root, "missing.mkv"), danglingInfo.LinkTarget);
    }

    [Fact]
    public void MoveFile_ReplacesOnlyWhenAsked()
    {
        var source = MakeFile("source.mkv", "new");
        var destination = MakeFile("destination.mkv", "old");

        Assert.Throws<IOException>(() => _fileSystem.MoveFile(source, destination));
        _fileSystem.MoveFile(source, destination, overwrite: true);

        Assert.False(File.Exists(source));
        Assert.Equal("new", File.ReadAllText(destination));
    }

    [Fact]
    public void MoveFile_ChangesTheCaseOfAName()
    {
        var source = MakeFile("afile.mkv");
        var destination = Path.Combine(_root, "AFile.mkv");

        _fileSystem.MoveFile(source, destination);

        Assert.Equal(["AFile.mkv"], Directory.GetFiles(_root).Select(path => Path.GetFileName(path)!).ToArray());
    }

    [Fact]
    public void MoveDirectory_RenamesOrRefusesAnExistingOne()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        MakeFile(Path.Combine("source", "video.mkv"));
        var existing = Directory.CreateDirectory(Path.Combine(_root, "existing")).FullName;
        var destination = Path.Combine(_root, "destination");

        Assert.Throws<IOException>(() => _fileSystem.MoveDirectory(source, existing));
        Assert.Throws<IOException>(() => _fileSystem.MoveDirectory(source, Path.Combine(source, "inside")));
        _fileSystem.MoveDirectory(source, destination);

        Assert.False(Directory.Exists(source));
        Assert.True(File.Exists(Path.Combine(destination, "video.mkv")));
    }

    [Fact]
    public void MoveDirectory_MergesWhenAsked()
    {
        Directory.CreateDirectory(Path.Combine(_root, "source", "sub"));
        Directory.CreateDirectory(Path.Combine(_root, "destination", "sub"));
        MakeFile(Path.Combine("source", "same.mkv"), "new");
        MakeFile(Path.Combine("destination", "same.mkv"), "old");
        MakeFile(Path.Combine("source", "sub", "a.mkv"));
        MakeFile(Path.Combine("destination", "sub", "b.mkv"));

        _fileSystem.MoveDirectory(Path.Combine(_root, "source"), Path.Combine(_root, "destination"), overwrite: true);

        Assert.False(Directory.Exists(Path.Combine(_root, "source")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(_root, "destination", "same.mkv")));
        Assert.True(File.Exists(Path.Combine(_root, "destination", "sub", "a.mkv")));
        Assert.True(File.Exists(Path.Combine(_root, "destination", "sub", "b.mkv")));
    }

    [Fact]
    public void MoveDirectory_MovesLinksAsLinksAndCrossesVolumes()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux() && Directory.Exists("/dev/shm"), "Needs a second volume.");
        var outside = Directory.CreateDirectory(Path.Combine(_root, "outside")).FullName;
        MakeFile(Path.Combine("outside", "kept.mkv"));
        var source = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        MakeFile(Path.Combine("source", "video.mkv"));
        _fileSystem.CreateSymbolicLink(outside, Path.Combine(source, "linked"));
        var destination = Path.Combine("/dev/shm", $"shoko-fs-{Guid.NewGuid():N}");
        try
        {
            _fileSystem.MoveDirectory(source, destination);

            Assert.False(Directory.Exists(source));
            Assert.True(File.Exists(Path.Combine(destination, "video.mkv")));
            Assert.Equal(outside, File.ResolveLinkTarget(Path.Combine(destination, "linked"), false)?.FullName);
            Assert.True(File.Exists(Path.Combine(outside, "kept.mkv")));
        }
        finally
        {
            if (Directory.Exists(destination))
                Directory.Delete(destination, true);
        }
    }

    [Fact]
    public void CanCreateSymbolicLinks_TestsTheDirectoryAndCleansUp()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, "folder")).FullName;
        bool expected;
        try
        {
            File.WriteAllText(Path.Combine(_root, "target"), string.Empty);
            File.CreateSymbolicLink(Path.Combine(_root, "link"), Path.Combine(_root, "target"));
            expected = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            expected = false;
        }

        Assert.Equal(expected, _fileSystem.CanCreateSymbolicLinks(folder));
        Assert.Empty(Directory.GetFileSystemEntries(folder));
        Assert.Throws<DirectoryNotFoundException>(() => _fileSystem.CanCreateSymbolicLinks(Path.Combine(_root, "missing")));
    }

    [Fact]
    public void GetDirectoryPaths_ListsOnlyTheTopLevelUnlessRecursive()
    {
        Directory.CreateDirectory(Path.Combine(_root, "a", "b"));
        Directory.CreateDirectory(Path.Combine(_root, "c"));

        var topLevel = _fileSystem.GetDirectoryPaths(_root, cancellationToken: TestContext.Current.CancellationToken).Select(name => Path.GetFileName(name)!).Order().ToArray();
        var all = _fileSystem.GetDirectoryPaths(_root, recursive: true, cancellationToken: TestContext.Current.CancellationToken).Select(name => Path.GetFileName(name)!).Order().ToArray();

        Assert.Equal(["a", "c"], topLevel);
        Assert.Equal(["a", "b", "c"], all);
    }

    [Fact]
    public void GetFileSystemEntries_ListsFilesAndDirectories()
    {
        Directory.CreateDirectory(Path.Combine(_root, "a"));
        MakeFile(Path.Combine("a", "one.mkv"));
        MakeFile("two.txt");

        var entries = _fileSystem.GetFileSystemEntries(_root, recursive: true, extensions: [".mkv"], cancellationToken: TestContext.Current.CancellationToken)
            .Select(entry => entry.Path)
            .Select(path => Path.GetRelativePath(_root, path))
            .Order()
            .ToArray();

        Assert.Equal(["a", Path.Combine("a", "one.mkv")], entries);
    }

    [Fact]
    public void FindHardLinkedVideo_JoinsAHardLinkButNotACopy()
    {
        var source = MakeFile("source.mkv");
        var link = Path.Combine(_root, "link.mkv");
        var copy = Path.Combine(_root, "copy.mkv");
        _fileSystem.CreateHardLink(source, link);
        _fileSystem.CopyFile(source, copy);
        var video = new VideoLocal { Hash = "ED2K", FileSize = 5 };

        Assert.Same(video, VideoService.FindHardLinkedVideo(_fileSystem, link, [(video, source)]));
        Assert.Null(VideoService.FindHardLinkedVideo(_fileSystem, copy, [(video, source)]));
        Assert.Null(VideoService.FindHardLinkedVideo(_fileSystem, source, [(video, source)]));
    }
}
