using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// A rehash that puts a file's location on another video record removes the
/// record it came from once no location points at it, so no empty record is
/// left in the unrecognized files.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class RehashLeftoverRecordTests(DatabaseMigrationFixture fixture) : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("shoko-rehash-").FullName;

    private ShokoManagedFolder? _folder;

    public void Dispose()
    {
        if (_folder is not null)
        {
            var videoService = (VideoService)fixture.Services.GetRequiredService<IVideoService>();
            foreach (var place in fixture.Services.GetRequiredService<VideoLocal_PlaceRepository>().GetByManagedFolderID(_folder.ID).ToList())
                videoService.RemoveRecord(place, skipEvents: true).GetAwaiter().GetResult();
            fixture.Services.GetRequiredService<ShokoManagedFolderRepository>().Delete(_folder);
        }

        Directory.Delete(_root, recursive: true);
    }

    private IVideoHashingService Hashing
        => fixture.Services.GetRequiredService<IVideoHashingService>();

    private VideoLocalRepository Videos
        => fixture.Services.GetRequiredService<VideoLocalRepository>();

    private string AddFile(string name, byte content)
    {
        if (_folder is null)
        {
            _folder = new ShokoManagedFolder { Name = $"Rehash {Guid.NewGuid():N}", Path = _root };
            fixture.Services.GetRequiredService<ShokoManagedFolderRepository>().Save(_folder);
        }

        var path = Path.Join(_root, name);
        Write(path, content);
        return path;
    }

    private static void Write(string path, byte content)
        => File.WriteAllBytes(path, Enumerable.Repeat(content, 4096).ToArray());

    private async Task<int> Hash(string path, bool useExistingHashes = true)
        => ((VideoLocal)(await Hashing.GetHashesForPath(path, useExistingHashes, skipFindRelease: true, skipEvents: true)).Video).VideoLocalID;

    [Fact]
    public async Task ARehashToNewContentRemovesTheRecordItLeft()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var path = AddFile("episode.mkv", 1);
        var before = await Hash(path);

        Write(path, 2);
        var after = await Hash(path, useExistingHashes: false);

        Assert.NotEqual(before, after);
        Assert.Null(Videos.GetByID(before));
        Assert.Single(Videos.GetByID(after)!.Places);
    }

    [Fact]
    public async Task ARecordWithAnotherCopyIsKept()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var changed = AddFile("episode.mkv", 3);
        var copy = AddFile("copy.mkv", 3);
        var before = await Hash(changed);
        Assert.Equal(before, await Hash(copy));

        Write(changed, 4);
        var after = await Hash(changed, useExistingHashes: false);

        Assert.NotEqual(before, after);
        var kept = Videos.GetByID(before);
        Assert.NotNull(kept);
        Assert.Equal(copy, Assert.Single(kept.Places).Path);
    }
}
