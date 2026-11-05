using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Shoko.BuildTools;
using Shoko.Server.Plugin.Models;
using Xunit;

namespace Shoko.Tests.BuildTools;

/// <summary>
///   Covers an archive's packed and unpacked sizes on their way from
///   <c>shoko-build</c>'s release entry to the server, which reads a missing
///   one as <c>0</c>.
/// </summary>
public class ManifestArchiveSizeTests : IDisposable
{
    private readonly string _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"shoko-manifest-size-{Guid.NewGuid():N}")).FullName;

    public void Dispose()
        => Directory.Delete(_root, recursive: true);

    [Theory]
    [InlineData(1234L, 5678L)]
    [InlineData(null, null)]
    public void TheArchiveEntryCarriesTheSizesToTheServer(long? packedSize, long? unpackedSize)
    {
        var path = Path.Combine(_root, "manifest.json");
        var manifest = new Manifest { ID = Guid.NewGuid(), Name = "Example" };
        ManifestManager.UpdateOrAddRelease(manifest, new Version(1, 0, 0), new Version(6, 0, 0), "any", [], packedSize: packedSize, unpackedSize: unpackedSize);
        ManifestManager.Save(manifest, path);

        var json = File.ReadAllText(path);
        Assert.Equal(packedSize is not null, json.Contains("\"packed_size\""));
        Assert.Equal(unpackedSize is not null, json.Contains("\"unpacked_size\""));

        var archive = JsonConvert.DeserializeObject<RemotePackageManifestInfo>(json)!.Releases!.Single().Archives.Single();
        Assert.Equal(packedSize ?? 0, archive.PackedSize);
        Assert.Equal(unpackedSize ?? 0, archive.UnpackedSize);
    }
}
