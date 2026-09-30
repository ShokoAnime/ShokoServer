using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Namotion.Reflection;
using NJsonSchema;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Server.API.v3.Controllers;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
///   Covers searching plugins over more than their name, and the tiered
///   search that ranks a name hit above a word found in a description.
/// </summary>
public class PluginSearchTests
{
    #region Tiered search

    private sealed record Entry(string Name, string Description, IReadOnlyList<string> Tags, string? Authors);

    private static readonly IReadOnlyList<Func<Entry, IEnumerable<string?>>> _tiers =
    [
        e => [e.Name],
        e => [.. e.Tags, e.Authors],
        e => [e.Description],
    ];

    [Fact]
    public void Tiered_NameHitLaterInTheName_RanksAboveDescriptionHitAtTheStart()
    {
        var byName = new Entry("Shoko Anime Tracker", "Keeps a list of what you watched.", [], null);
        var byDescription = new Entry("Something Else", "Tracker for everything under the sun, with a very long description.", [], null);

        var results = new[] { byDescription, byName }.Search("tracker", _tiers).Select(r => r.Result).ToList();

        Assert.Equal([byName, byDescription], results);
    }

    [Fact]
    public void Tiered_TagOrAuthorHit_RanksBetweenNameAndDescription()
    {
        var byName = new Entry("Scrobbler", "Nothing to see.", [], null);
        var byTag = new Entry("Other", "Nothing to see.", ["scrobbler"], null);
        var byAuthor = new Entry("Third", "Nothing to see.", [], "The Scrobbler Team");
        var byDescription = new Entry("Fourth", "Scrobbler for your library.", [], null);

        var results = new[] { byDescription, byAuthor, byTag, byName }.Search("scrobbler", _tiers).Select(r => r.Result).ToList();

        Assert.Equal([byName, byTag, byAuthor, byDescription], results);
        Assert.Equal([0, 1, 1, 2], new[] { byDescription, byAuthor, byTag, byName }.Search("scrobbler", _tiers).Select(r => r.Tier));
    }

    [Fact]
    public void Tiered_SkipsMissingFields_AndDropsItemsWithoutAHit()
    {
        var withoutAuthors = new Entry("Plain", "A renamer.", ["files"], null);
        var unrelated = new Entry("Other", "Something unrelated.", [], null);

        var results = new[] { unrelated, withoutAuthors }.Search("renamer", _tiers).ToList();

        var result = Assert.Single(results);
        Assert.Same(withoutAuthors, result.Result);
        Assert.Equal(2, result.Tier);
    }

    [Fact]
    public void SingleSelector_KeepsRankingByPositionAlone()
    {
        // One flat group: every hit is tier 0, so the earliest match still wins as before.
        var byName = new Entry("Shoko Anime Tracker", "Keeps a list of what you watched.", [], null);
        var byDescription = new Entry("Something Else", "Tracker for everything.", [], null);

        var results = new[] { byName, byDescription }
            .Search("tracker", e => [e.Name, e.Description])
            .ToList();

        Assert.Equal([byDescription, byName], results.Select(r => r.Result));
        Assert.All(results, r => Assert.Equal(0, r.Tier));
    }

    #endregion

    #region Plugin controller

    private static PluginController CreateController(IReadOnlyList<LocalPluginInfo> plugins)
    {
        var pluginManager = new Mock<IPluginManager>();
        pluginManager.Setup(m => m.GetPluginInfos()).Returns(plugins);
        return new PluginController(
            Mock.Of<ISettingsProvider>(),
            Mock.Of<IApplicationPaths>(),
            pluginManager.Object,
            Mock.Of<IPluginDependencyResolver>(),
            NullLogger<PluginController>.Instance
        );
    }

    [Fact]
    public void GetPlugins_FindsDescriptionTagsAndAuthors_NameHitFirst()
    {
        var byDescription = PluginTestDoubles.DescribedPluginInfo(Guid.NewGuid(), "Alpha", "Relocates files by a script.", [], null);
        var byTag = PluginTestDoubles.DescribedPluginInfo(Guid.NewGuid(), "Beta", string.Empty, ["relocation", "script"], null);
        var byAuthor = PluginTestDoubles.DescribedPluginInfo(Guid.NewGuid(), "Gamma", string.Empty, [], "Script Writers");
        var byName = PluginTestDoubles.DescribedPluginInfo(Guid.NewGuid(), "Zeta Script Renamer", string.Empty, [], null);
        var unrelated = PluginTestDoubles.DescribedPluginInfo(Guid.NewGuid(), "Omega", "Shows a calendar.", ["airing"], "Someone");
        var controller = CreateController([byDescription, byTag, byAuthor, byName, unrelated]);

        var results = controller.GetPlugins(query: "script").Value!;

        Assert.Equal([byName.ID, byTag.ID, byAuthor.ID, byDescription.ID], results.Select(p => p.ID));
    }

    [Fact]
    public void GetPluginPages_PutsANameHitBeforeADescriptionHit()
    {
        var byDescription = PluginTestDoubles.PagedPluginInfo(Guid.NewGuid(), "Alpha", "Adds a calendar page.", "alpha-page");
        var byName = PluginTestDoubles.PagedPluginInfo(Guid.NewGuid(), "Calendar", string.Empty, "calendar-page");
        var controller = CreateController([byDescription, byName]);

        var results = controller.GetPluginPages(query: "calendar");

        Assert.Equal(["calendar-page", "alpha-page"], results.Select(page => page.Name));
    }

    #endregion

    #region Configuration controller

    private static ConfigurationInfo Configuration(IConfigurationService service, LocalPluginInfo plugin, string name, string description)
        => new(service)
        {
            ID = Guid.NewGuid(),
            Path = null,
            Name = name,
            Description = description,
            HasCustomActions = false,
            HasCustomNewFactory = false,
            HasCustomValidation = false,
            HasCustomSave = false,
            HasCustomLoad = false,
            HasLiveEdit = false,
            Type = typeof(object),
            ContextualType = typeof(object).ToContextualType(),
            Schema = new JsonSchema(),
            PluginInfo = plugin,
        };

    [Fact]
    public void GetConfigurations_KeepsTheirPluginGroups_NameHitFirstWithinOne()
    {
        var service = new Mock<IConfigurationService>();
        service.Setup(s => s.RestartPendingFor).Returns(new Dictionary<Guid, IReadOnlySet<string>>());
        service.Setup(s => s.LoadedEnvironmentVariables).Returns(new Dictionary<Guid, IReadOnlySet<string>>());
        var alpha = PluginTestDoubles.DescribedPluginInfo(Guid.NewGuid(), "Alpha", string.Empty, [], null);
        var beta = PluginTestDoubles.DescribedPluginInfo(Guid.NewGuid(), "Beta", string.Empty, [], null);
        var general = Configuration(service.Object, alpha, "General", "Where every image is stored.");
        var images = Configuration(service.Object, alpha, "Images", string.Empty);
        var imageSettings = Configuration(service.Object, beta, "Image Settings", string.Empty);
        service.Setup(s => s.GetAllConfigurationInfos()).Returns([general, imageSettings, images]);
        var controller = new ConfigurationController(Mock.Of<ISettingsProvider>(), Mock.Of<IPluginManager>(), service.Object);

        var results = controller.GetConfigurations(query: "image").Value!;

        Assert.Equal(["Images", "General", "Image Settings"], results.Select(c => c.Name));
    }

    #endregion
}
