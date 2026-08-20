using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.BuildTools;
using Xunit;

namespace Shoko.Tests.BuildTools;

/// <summary>
/// Covers what <see cref="ManifestManager.PruneReleases"/> reports. The method used to
/// return <c>void</c>, so a destructive step left no record of what it dropped; it now
/// hands the caller the removed entries so they can be named on the console.
/// </summary>
public class ManifestPruneTests
{
    private static readonly DateTime _epoch = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Manifest BuildManifest(params (string Version, ReleaseChannel Channel)[] releases)
        => new()
        {
            ID = Guid.NewGuid(),
            Name = "Test Plugin",
            Releases = releases
                .Select((r, i) => new ManifestRelease
                {
                    Version = r.Version,
                    Channel = r.Channel,
                    ReleasedAt = _epoch.AddDays(i),
                })
                .Reverse()
                .ToList(),
        };

    [Fact]
    public void NoReleases_DropsNothing()
    {
        var manifest = new Manifest { ID = Guid.NewGuid(), Name = "Test Plugin" };

        Assert.Empty(ManifestManager.PruneReleases(manifest, 5, "channel"));
        Assert.Null(manifest.Releases);
    }

    [Theory]
    [InlineData("channel")]
    [InlineData("global")]
    public void FewerReleasesThanTheLimit_DropsNothing(string method)
    {
        var manifest = BuildManifest(("1.0.0", ReleaseChannel.Stable), ("1.1.0", ReleaseChannel.Stable));
        var releases = manifest.Releases;

        Assert.Empty(ManifestManager.PruneReleases(manifest, 5, method));
        Assert.Same(releases, manifest.Releases);
    }

    [Theory]
    [InlineData("channel")]
    [InlineData("global")]
    public void SingleChannel_ReturnsTheOldestEntriesNewestFirst(string method)
    {
        var manifest = BuildManifest(
            ("0.14.0", ReleaseChannel.Stable),
            ("0.15.0", ReleaseChannel.Stable),
            ("0.16.0", ReleaseChannel.Stable),
            ("0.17.0", ReleaseChannel.Stable));

        var dropped = ManifestManager.PruneReleases(manifest, 2, method);

        Assert.Equal(["0.15.0", "0.14.0"], dropped.Select(r => r.Version));
        Assert.All(dropped, r => Assert.Equal(ReleaseChannel.Stable, r.Channel));
        Assert.Equal(["0.17.0", "0.16.0"], manifest.Releases!.Select(r => r.Version));
    }

    [Fact]
    public void ChannelMethod_ReturnsWhatEachChannelDropped()
    {
        var manifest = BuildManifest(
            ("1.0.0", ReleaseChannel.Stable),
            ("1.0.1", ReleaseChannel.Dev),
            ("1.1.0", ReleaseChannel.Stable),
            ("1.1.1", ReleaseChannel.Dev),
            ("1.2.0", ReleaseChannel.Stable),
            ("1.2.1", ReleaseChannel.Dev));

        var dropped = ManifestManager.PruneReleases(manifest, 2, "channel");

        Assert.Equal(
            [("1.0.1", ReleaseChannel.Dev), ("1.0.0", ReleaseChannel.Stable)],
            dropped.Select(r => (r.Version, r.Channel)));
        Assert.Equal(4, manifest.Releases!.Count);
    }

    [Fact]
    public void GlobalMethod_ReturnsEverythingBelowTheCutoffAcrossChannels()
    {
        var manifest = BuildManifest(
            ("1.0.0", ReleaseChannel.Stable),
            ("1.0.1", ReleaseChannel.Dev),
            ("1.1.0", ReleaseChannel.Stable),
            ("1.1.1", ReleaseChannel.Dev),
            ("1.2.0", ReleaseChannel.Stable),
            ("1.2.1", ReleaseChannel.Dev));

        var dropped = ManifestManager.PruneReleases(manifest, 2, "global");

        Assert.Equal(
            [
                ("1.1.1", ReleaseChannel.Dev),
                ("1.1.0", ReleaseChannel.Stable),
                ("1.0.1", ReleaseChannel.Dev),
                ("1.0.0", ReleaseChannel.Stable),
            ],
            dropped.Select(r => (r.Version, r.Channel)));
        Assert.Equal(["1.2.1", "1.2.0"], manifest.Releases!.Select(r => r.Version));
    }

    [Fact]
    public void DroppedEntriesAreTheInstancesRemovedFromTheManifest()
    {
        var manifest = BuildManifest(
            ("1.0.0", ReleaseChannel.Stable),
            ("1.1.0", ReleaseChannel.Stable),
            ("1.2.0", ReleaseChannel.Stable));
        var before = manifest.Releases!.ToList();

        var dropped = ManifestManager.PruneReleases(manifest, 1, "global");

        Assert.Equal(before.Count, dropped.Count + manifest.Releases!.Count);
        Assert.Empty(dropped.Intersect(manifest.Releases));
        Assert.Empty(before.Except(dropped.Concat(manifest.Releases)));
    }

    [Fact]
    public void UndatedReleasesAreDroppedBeforeDatedOnes()
    {
        var manifest = BuildManifest(("1.0.0", ReleaseChannel.Stable), ("1.1.0", ReleaseChannel.Stable));
        manifest.Releases!.Add(new ManifestRelease { Version = "0.9.0", Channel = ReleaseChannel.Stable });

        var dropped = ManifestManager.PruneReleases(manifest, 2, "channel");

        Assert.Equal(["0.9.0"], dropped.Select(r => r.Version));
    }
}
