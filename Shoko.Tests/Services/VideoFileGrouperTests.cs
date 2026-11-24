using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Shoko.Abstractions.Video;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.Tests.Services;

public sealed class VideoFileGrouperTests : IDisposable
{
    private readonly FileSystemService _fileSystem = new(NullLogger<FileSystemService>.Instance);

    private readonly string _root = Directory.CreateTempSubdirectory("shoko-groups-").FullName;

    public void Dispose()
        => Directory.Delete(_root, true);

    private bool CanLink
        => _fileSystem.CanCreateSymbolicLinks(_root);

    private string MakeFile(string name, string contents = "video")
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, contents);
        return path;
    }

    private static VideoLocal_Place Stored(int id, int primary, bool isKept = false)
        => new() { ID = id, PrimaryLocationID = primary, IsKept = isKept, VideoID = 1, RelativePath = $"{id}.mkv" };

    private Dictionary<int, int> Primaries(params (int ID, string Path)[] locations)
        => VideoFileGrouper.FindPrimaries(locations.Select(tuple => (Stored(tuple.ID, 0), VideoFileGrouper.Read(_fileSystem, tuple.Path))).ToList());

    private static int[] Ids(VideoFileGroup group)
        => group.Files.Select(file => file.ID).ToArray();

    [Fact]
    public void Read_TellsLinksAndTheirTargets()
    {
        Assert.SkipUnless(CanLink, "This user may not make symbolic links here.");
        var file = MakeFile("file.mkv");
        var link = Path.Combine(_root, "link.mkv");
        var broken = Path.Combine(_root, "broken.mkv");
        _fileSystem.CreateSymbolicLink(file, link);
        File.CreateSymbolicLink(broken, Path.Combine(_root, "missing.mkv"));

        var plain = VideoFileGrouper.Read(_fileSystem, file);
        var linked = VideoFileGrouper.Read(_fileSystem, link);
        var dead = VideoFileGrouper.Read(_fileSystem, broken);

        Assert.True(plain is { Exists: true, IsSymbolicLink: false, SymbolicLinkTarget: null, Identity: not null });
        Assert.True(linked is { Exists: true, IsSymbolicLink: true });
        Assert.Equal(file, linked.SymbolicLinkTarget);
        Assert.Equal(plain.Identity, linked.Identity);
        Assert.True(dead is { Exists: true, IsSymbolicLink: true, Identity: null });
        Assert.False(VideoFileGrouper.Read(_fileSystem, Path.Combine(_root, "nothing.mkv")).Exists);
    }

    [Fact]
    public void FindPrimaries_JoinsLinksUnderTheFileItself()
    {
        Assert.SkipUnless(CanLink, "This user may not make symbolic links here.");
        var original = MakeFile("original.mkv");
        var hardLink = Path.Combine(_root, "hard-link.mkv");
        var symbolicLink = Path.Combine(_root, "symbolic-link.mkv");
        _fileSystem.CreateHardLink(original, hardLink);
        _fileSystem.CreateSymbolicLink(original, symbolicLink);
        var copy = MakeFile("copy.mkv");

        var primaries = Primaries((1, symbolicLink), (2, original), (3, hardLink), (4, copy));

        Assert.Equal(new Dictionary<int, int> { [1] = 2, [2] = 2, [3] = 2, [4] = 4 }, primaries);
    }

    [Fact]
    public void FindPrimaries_HandsALinksCopyToItsFileOnceThatIsAdded()
    {
        Assert.SkipUnless(CanLink, "This user may not make symbolic links here.");
        var original = MakeFile("original.mkv");
        var symbolicLink = Path.Combine(_root, "symbolic-link.mkv");
        _fileSystem.CreateSymbolicLink(original, symbolicLink);

        Assert.Equal(new Dictionary<int, int> { [1] = 1 }, Primaries((1, symbolicLink)));
        Assert.Equal(new Dictionary<int, int> { [1] = 2, [2] = 2 }, Primaries((1, symbolicLink), (2, original)));
    }

    [Fact]
    public void FindPrimaries_LeavesABrokenLinkACopyOfItsOwn()
    {
        Assert.SkipUnless(CanLink, "This user may not make symbolic links here.");
        var file = MakeFile("file.mkv");
        var broken = Path.Combine(_root, "broken.mkv");
        File.CreateSymbolicLink(broken, Path.Combine(_root, "missing.mkv"));

        Assert.Equal(new Dictionary<int, int> { [1] = 1, [2] = 2 }, Primaries((1, file), (2, broken)));
    }

    [Fact]
    public void Group_ReadsTheStoredGroups_ThePrimaryFirst()
    {
        var groups = VideoFileGrouper.Group([Stored(1, 2), Stored(2, 2), Stored(3, 2), Stored(4, 4)], joinLinks: true, firstAvailableFileID: 2, ignoreKept: false);

        Assert.Equal(2, groups.Count);
        Assert.Equal(2, groups[0].ID);
        Assert.Equal([2, 1, 3], Ids(groups[0]));
        Assert.True(groups[0] is { IsLinked: true, IsPreferred: true });
        Assert.True(groups[1] is { ID: 4, IsPreferred: false });
    }

    [Fact]
    public void Group_KeepsEveryLocationApart_WhenLinksAreNotJoined()
    {
        var groups = VideoFileGrouper.Group([Stored(1, 2), Stored(2, 2), Stored(3, 2), Stored(4, 4)], joinLinks: false, firstAvailableFileID: 2, ignoreKept: false);

        Assert.Equal([2, 1, 3, 4], groups.Select(group => group.ID).ToArray());
        Assert.Equal([true, false, false, false], groups.Select(group => group.IsPreferred).ToArray());
    }

    [Fact]
    public void Group_PrefersEveryKeptCopy_UnlessKeptIsIgnored()
    {
        VideoLocal_Place[] files = [Stored(1, 2), Stored(2, 2), Stored(3, 3, isKept: true), Stored(4, 4, isKept: true), Stored(5, 5)];

        var kept = VideoFileGrouper.Group(files, joinLinks: true, firstAvailableFileID: 2, ignoreKept: false);
        var ignored = VideoFileGrouper.Group(files, joinLinks: true, firstAvailableFileID: 2, ignoreKept: true);

        Assert.Equal([3, 4], kept.Where(group => group.IsPreferred).Select(group => group.ID).ToArray());
        Assert.All(kept.Where(group => group.IsPreferred), group => Assert.True(group.IsKept));
        Assert.Equal([2], ignored.Where(group => group.IsPreferred).Select(group => group.ID).ToArray());
        Assert.All(ignored, group => Assert.False(group.IsKept));
    }

    [Fact]
    public void Group_MakesALocationWhosePrimaryIsElsewhereACopyOfItsOwn()
    {
        var groups = VideoFileGrouper.Group([Stored(1, 1), Stored(2, 9), Stored(0, 0)], joinLinks: true, firstAvailableFileID: 1, ignoreKept: false);

        Assert.Equal([1, 0, 2], groups.Select(group => group.ID).ToArray());
        Assert.True(groups[0].IsPreferred);
    }
}
