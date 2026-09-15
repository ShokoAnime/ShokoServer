using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Enums;
using Shoko.Abstractions.Plugin.Models;
using Xunit;

namespace Shoko.Tests.Plugin;

public class PluginFeatureTests
{
    [Theory]
    [InlineData("password-reset")]
    [InlineData("v2")]
    [InlineData("a")]
    [InlineData("username-recovery-by-email")]
    public void IsValid_AcceptsKebabCaseNames(string name)
        => Assert.True(PluginFeature.IsValid(new() { Name = name }, out _));

    [Theory]
    [InlineData("")]
    [InlineData("Password-Reset")]
    [InlineData("password_reset")]
    [InlineData("password reset")]
    [InlineData("-password")]
    [InlineData("password-")]
    [InlineData("password--reset")]
    [InlineData("pässword")]
    public void IsValid_RejectsNamesThatAreNotKebabCase(string name)
    {
        Assert.False(PluginFeature.IsValid(new() { Name = name }, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void IsValid_RejectsNamesAboveTheMaxLength()
    {
        Assert.True(PluginFeature.IsValid(new() { Name = new('a', PluginFeature.MaxNameLength) }, out _));
        Assert.False(PluginFeature.IsValid(new() { Name = new('a', PluginFeature.MaxNameLength + 1) }, out _));
    }

    [Fact]
    public void IsValid_RejectsUndefinedVisibilityAndMissingVersion()
    {
        Assert.False(PluginFeature.IsValid(new() { Name = "feature", Visibility = default }, out _));
        Assert.False(PluginFeature.IsValid(new() { Name = "feature", Version = null! }, out _));
        Assert.False(PluginFeature.IsValid(null, out _));
    }

    [Fact]
    public void Defaults_AreVersionOneAndAuthenticated()
    {
        var feature = new PluginFeature { Name = "feature" };

        Assert.Equal(new Version(1, 0, 0), feature.Version);
        Assert.Equal(PluginFeatureVisibility.Authenticated, feature.Visibility);
        Assert.Null(feature.Metadata);
    }

    [Fact]
    public void GetFeatures_NormalizesTheVersionToMajorMinorPatch()
    {
        var pluginInfo = MakePlugin(
            new() { Name = "two-part", Version = new(2, 1) },
            new() { Name = "four-part", Version = new(3, 2, 1, 9) }
        );

        var features = pluginInfo.GetFeatures();

        Assert.Equal(new Version(2, 1, 0), features[0].Version);
        Assert.Equal(new Version(3, 2, 1), features[1].Version);
    }

    [Fact]
    public void GetFeatures_DropsInvalidAndDuplicateFeaturesAndKeepsTheOrder()
    {
        var metadata = new JObject { ["cooldown"] = 60 };
        var pluginInfo = MakePlugin(
            new() { Name = "second", Visibility = PluginFeatureVisibility.Anonymous, Metadata = metadata },
            new() { Name = "Not-Valid" },
            new() { Name = "first" },
            new() { Name = "second", Visibility = PluginFeatureVisibility.Admin }
        );
        var dropped = new List<(string? Name, string Reason)>();

        var features = pluginInfo.GetFeatures((feature, reason) => dropped.Add((feature?.Name, reason)));

        Assert.Equal(["second", "first"], features.Select(feature => feature.Name));
        Assert.Equal(PluginFeatureVisibility.Anonymous, features[0].Visibility);
        Assert.Same(metadata, features[0].Metadata);
        Assert.All(features, feature => Assert.Same(pluginInfo, feature.PluginInfo));
        Assert.Equal(["Not-Valid", "second"], dropped.Select(entry => entry.Name));
    }

    [Fact]
    public void GetFeatures_IsEmptyForAnInactivePlugin()
    {
        var pluginInfo = MakePlugin();

        Assert.Empty(pluginInfo.GetFeatures());
    }

    private static LocalPluginInfo MakePlugin(params PluginFeature[] features)
    {
        Mock<IPlugin>? plugin = null;
        if (features.Length > 0)
        {
            plugin = new Mock<IPlugin>();
            plugin.Setup(p => p.GetFeatures()).Returns(features);
        }

        return new()
        {
            ID = Guid.NewGuid(),
            Name = "Test",
            Description = string.Empty,
            Version = new()
            {
                Version = new(1, 0, 0),
                RuntimeIdentifier = "any",
                AbstractionVersion = new(6, 0, 0),
                SourceRevision = null,
                ReleaseTag = null,
                Channel = ReleaseChannel.Stable,
                ReleasedAt = DateTime.UnixEpoch,
            },
            Authors = null,
            RepositoryUrl = null,
            HomepageUrl = null,
            Tags = [],
            Thumbnail = null,
            InstalledAt = DateTime.UnixEpoch,
            IsEnabled = true,
            IsPinned = false,
            IsActive = plugin is not null,
            CanLoad = true,
            CanUninstall = true,
            Plugin = plugin?.Object,
            PluginType = plugin?.Object.GetType(),
            ServiceRegistrationType = null,
            ApplicationRegistrationType = null,
            ContainingDirectory = null,
            DLLs = ["Test.dll"],
            Types = [],
            Dependencies = [],
        };
    }
}
