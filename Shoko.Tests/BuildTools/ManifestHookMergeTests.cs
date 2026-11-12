using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Shoko.BuildTools;
using Xunit;

namespace Shoko.Tests.BuildTools;

/// <summary>
///   Covers reading back the pending files the <c>ShokoBeforeAssetWrite</c>
///   and <c>ShokoBeforeReleaseWrite</c> hooks edited: only <c>extensions</c>
///   are taken, merged key by key.
/// </summary>
public class ManifestHookMergeTests : IDisposable
{
    private readonly string _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"shoko-manifest-hook-{Guid.NewGuid():N}")).FullName;

    public void Dispose()
        => Directory.Delete(_root, recursive: true);

    [Fact]
    public void OnlyTheAssetExtensionsAreTakenBack()
    {
        var release = NewRelease();
        var archive = release.Archives!.Single(a => a.Runtime == "win-x64");
        var path = Path.Combine(_root, "pending.json");
        ManifestHooks.WritePendingAsset(archive, path);

        var pending = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        Assert.Equal("win-x64", pending["runtime"]!.GetValue<string>());
        pending["checksum"] = "sha256:changed";
        pending["extensions"] = JsonNode.Parse("""{"mine":{"size":3}}""");
        File.WriteAllText(path, pending.ToJsonString());

        Assert.Null(ManifestHooks.MergePendingAsset(archive, path));
        Assert.Equal("sha256:bb", archive.Checksum);
        Assert.Equal("""{"mine":{"size":3}}""", archive.Extra!["extensions"].GetRawText());
    }

    [Fact]
    public void OnlyTheReleaseAndArchiveExtensionsAreTakenBackMergedKeyByKey()
    {
        var release = NewRelease();
        release.Extra = new() { ["extensions"] = JsonNode.Parse("""{"other":{"kept":1},"mine":{"old":true,"gone":1}}""").Deserialize<JsonElement>() };
        var path = Path.Combine(_root, "pending.json");
        ManifestHooks.WritePendingRelease(release, path);

        var pending = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        Assert.Equal(2, pending["archives"]!.AsArray().Count);
        pending["version"] = "9.9.9";
        pending["extensions"] = JsonNode.Parse("""{"mine":{"gone":null,"webPlugins":["a"]}}""");
        pending["archives"]![1]!["extensions"] = JsonNode.Parse("""{"mine":{"size":3}}""");
        File.WriteAllText(path, pending.ToJsonString());

        Assert.Null(ManifestHooks.MergePendingRelease(release, path));
        Assert.Equal("1.0.0.5", release.Version);
        Assert.Equal(
            """{"other":{"kept":1},"mine":{"old":true,"webPlugins":["a"]}}""",
            JsonNode.Parse(release.Extra!["extensions"].GetRawText())!.ToJsonString());
        Assert.Null(release.Archives![0].Extra);
        Assert.Equal("""{"mine":{"size":3}}""", release.Archives![1].Extra!["extensions"].GetRawText());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"extensions":{},"extensions":{}}""")]
    [InlineData("""{"extensions":[1]}""")]
    [InlineData("""{"archives":[{"runtime":"win-x64","extensions":"x"}]}""")]
    public void AnUnreadablePendingReleaseIsRefused(string content)
    {
        var release = NewRelease();
        var path = Path.Combine(_root, "pending.json");
        File.WriteAllText(path, content);

        Assert.NotNull(ManifestHooks.MergePendingRelease(release, path));
        Assert.Null(release.Extra);
        Assert.All(release.Archives!, archive => Assert.Null(archive.Extra));
    }

    private static ManifestRelease NewRelease()
    {
        var manifest = new Manifest { ID = Guid.NewGuid(), Name = "Example" };
        ManifestManager.UpdateOrAddRelease(manifest, new Version(1, 0, 0, 5), new Version(6, 0, 0), "linux-x64", [], checksum: "sha256:aa");
        return ManifestManager.UpdateOrAddRelease(manifest, new Version(1, 0, 0, 5), new Version(6, 0, 0), "win-x64", [], checksum: "sha256:bb");
    }
}
