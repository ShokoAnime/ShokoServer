using System;
using Shoko.BuildTools;
using Xunit;

namespace Shoko.Tests.BuildTools;

/// <summary>
/// The project is the truth for a plugin's identity: what it states is written
/// into the manifest, and what it does not state is left alone.
/// </summary>
public class ManifestIdentitySyncTests
{
    private static Manifest Manifest()
        => new() { ID = Guid.NewGuid(), Name = "Old Name", Overview = "Old overview.", Tags = ["old"] };

    [Fact]
    public void StatedIdentity_ReplacesTheManifests()
    {
        var manifest = Manifest();

        BuildCommand.SyncIdentity(
            manifest,
            ("New Name", "PluginName"),
            "New Name",
            ("New overview.", "PluginOverview"),
            "New overview.",
            ("new,tags", "PluginTags"),
            ["discovered", "new", "tags"]
        );

        Assert.Equal("New Name", manifest.Name);
        Assert.Equal("New overview.", manifest.Overview);
        Assert.Equal(["discovered", "new", "tags"], manifest.Tags);
    }

    [Fact]
    public void UnstatedIdentity_IsLeftAlone()
    {
        var manifest = Manifest();

        BuildCommand.SyncIdentity(manifest, null, "Old Name", null, "Old overview.", null, ["old", "discovered"]);

        Assert.Equal("Old Name", manifest.Name);
        Assert.Equal("Old overview.", manifest.Overview);
        Assert.Equal(["old"], manifest.Tags);
    }
}
