using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Matching;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Search;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers how <see cref="MetadataProviderManager"/> fills in what was never decided about a
/// source: every entity type goes to the first provider claiming it, and auto-linking to the first
/// provider able to do it, once, while an admin's decisions are kept.
/// </summary>
public sealed class ProviderClaimTests : IDisposable
{
    #region Fixture

    private static readonly Guid s_core = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly Guid s_plugin = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly Guid s_later = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly IReadOnlyDictionary<MetadataSource, SettingsMigrations.AutoLinkCarryOver> s_nothingCarried =
        new Dictionary<MetadataSource, SettingsMigrations.AutoLinkCarryOver>();

    private readonly string _dataPath = Path.Join(Path.GetTempPath(), $"shoko-provider-claim-tests-{Guid.NewGuid():N}");

    public ProviderClaimTests()
        => Directory.CreateDirectory(_dataPath);

    public void Dispose()
    {
        if (Directory.Exists(_dataPath))
            Directory.Delete(_dataPath, recursive: true);
    }

    private static MetadataProviderManager.ProviderClaim Claim(Guid id, MetadataSource source, bool links = true, bool autoLinkByDefault = true)
        => new(
            id,
            id.ToString()[..4],
            new HashSet<MetadataSource> { source },
            new HashSet<MetadataEntityType> { MetadataEntityType.Series, MetadataEntityType.Episode },
            links,
            autoLinkByDefault
        );

    /// <summary>
    /// A manager over the fixture's folder, standing in for one start of the server.
    /// </summary>
    private MetadataProviderManager Boot()
        => TestProviderManagers.Boot(_dataPath);

    /// <summary>
    /// The provider answering an entity type: the first enabled one in its order.
    /// </summary>
    private static Guid? Answering(MetadataSourceSettings decisions, MetadataEntityType entityType)
        => decisions.Providers[entityType].FirstOrDefault(slot => slot.IsEnabled)?.ProviderID;

    /// <summary>
    /// A provider that links series on the plugin source and auto-links it, doing nothing else.
    /// </summary>
    public abstract class Linker : IMetadataSeriesLinkingProvider, IMetadataAutoLinkingProvider
    {
        public abstract string Name { get; }

        public MetadataSource Source => TestSources.Plugin;

        public IReadOnlySet<MetadataEntityType> LinkableEntityTypes { get; } =
            new HashSet<MetadataEntityType> { MetadataEntityType.Series, MetadataEntityType.Episode };

        public Task<IReadOnlyList<MetadataAutoLinkCandidate>> FindAutoLinks(int anidbAnimeID, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MetadataAutoLinkCandidate>>([]);

        public Task RefreshSeries(MetadataGuid seriesID, MetadataRefreshOptions options, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<(IReadOnlyList<MetadataSeriesSearchResult> Page, int TotalCount)> SearchSeries(MetadataSearchOptions options, CancellationToken cancellationToken = default)
            => Task.FromResult<(IReadOnlyList<MetadataSeriesSearchResult>, int)>(([], 0));

        public Task<IReadOnlyList<EpisodeMatch>> MatchEpisodes(
            IAnidbAnime anime,
            IReadOnlyList<IAnidbEpisode> anidbEpisodes,
            MetadataGuid providerSeriesID,
            MetadataGuid? providerSeasonID = null,
            IReadOnlyList<IMetadataEpisodeCrossReference>? existing = null,
            bool? considerOtherLinks = null,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult<IReadOnlyList<EpisodeMatch>>([]);
    }

    /// <summary>
    /// The provider installed first, named to sort after the one installed later.
    /// </summary>
    public sealed class FirstLinker : Linker
    {
        public override string Name => "Zeta";
    }

    /// <summary>
    /// The provider installed later, sorting ahead of the first by name.
    /// </summary>
    public sealed class LaterLinker : Linker
    {
        public override string Name => "Alpha";
    }

    /// <summary>
    /// A provider that refreshes series on the plugin source and cannot auto-link.
    /// </summary>
    public sealed class Refresher : IMetadataSeriesProvider
    {
        public string Name => "Refresher";

        public MetadataSource Source => TestSources.Plugin;

        public Task RefreshSeries(MetadataGuid seriesID, MetadataRefreshOptions options, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    #endregion

    #region Seeding

    [Fact]
    public void TheFirstProviderOnANewSourceTakesEveryEntityTypeAndAutoLinking()
    {
        var settings = new MetadataServiceSettings();

        var seeded = MetadataProviderManager.SeedDecisions(settings, [Claim(s_plugin, TestSources.Plugin)], s_nothingCarried, out var autoLinkers);

        Assert.True(seeded);
        var decisions = settings.Sources[TestSources.Plugin];
        Assert.Equal(s_plugin, Answering(decisions, MetadataEntityType.Series));
        Assert.Equal(s_plugin, Answering(decisions, MetadataEntityType.Episode));
        Assert.Equal(s_plugin, decisions.AutoLinker);
        Assert.True(decisions.AutoLink);
        Assert.False(decisions.AutoLinkRestricted);
        Assert.Equal([(TestSources.Plugin, s_plugin.ToString()[..4])], autoLinkers);
    }

    [Fact]
    public void AProviderAskingToWaitTakesAutoLinkingWithoutLinkingOnItsOwn()
    {
        var settings = new MetadataServiceSettings();

        MetadataProviderManager.SeedDecisions(settings, [Claim(s_plugin, TestSources.Plugin, autoLinkByDefault: false)], s_nothingCarried, out _);

        Assert.Equal(s_plugin, settings.Sources[TestSources.Plugin].AutoLinker);
        Assert.False(settings.Sources[TestSources.Plugin].AutoLink);
    }

    [Fact]
    public void TheFirstOfTwoProvidersAbleToLinkTakesTheSourceAndTheOtherStandsBy()
    {
        var settings = new MetadataServiceSettings();

        MetadataProviderManager.SeedDecisions(settings, [Claim(s_core, TestSources.Plugin), Claim(s_plugin, TestSources.Plugin)], s_nothingCarried, out _);

        Assert.Equal([new(s_core, true), new(s_plugin, true)], settings.Sources[TestSources.Plugin].Providers[MetadataEntityType.Series]);
        Assert.Equal(s_core, settings.Sources[TestSources.Plugin].AutoLinker);
    }

    [Fact]
    public void AutoLinkingGoesToTheFirstProviderAbleToLink()
    {
        var settings = new MetadataServiceSettings();

        MetadataProviderManager.SeedDecisions(settings, [Claim(s_core, TestSources.Plugin, links: false), Claim(s_plugin, TestSources.Plugin)], s_nothingCarried, out _);

        Assert.Equal(s_core, Answering(settings.Sources[TestSources.Plugin], MetadataEntityType.Series));
        Assert.Equal(s_plugin, settings.Sources[TestSources.Plugin].AutoLinker);
    }

    [Fact]
    public void AProviderRegisteredLaterJoinsTheEndOfEachOrderAndNeverTakesOverByItself()
    {
        var settings = new MetadataServiceSettings();
        MetadataProviderManager.SeedDecisions(settings, [Claim(s_plugin, TestSources.Plugin)], s_nothingCarried, out _);
        settings.Sources[TestSources.Plugin].Providers[MetadataEntityType.Episode] = [new(s_plugin, false)];

        // Even registered ahead of the first, as a name sorting first would.
        MetadataProviderManager.SeedDecisions(
            settings,
            [Claim(s_later, TestSources.Plugin), Claim(s_plugin, TestSources.Plugin)],
            s_nothingCarried,
            out var autoLinkers
        );

        // It stands by where something answers, and stays off where nothing was to.
        var decisions = settings.Sources[TestSources.Plugin];
        Assert.Empty(autoLinkers);
        Assert.Equal([new(s_plugin, true), new(s_later, true)], decisions.Providers[MetadataEntityType.Series]);
        Assert.Equal([new(s_plugin, false), new(s_later, false)], decisions.Providers[MetadataEntityType.Episode]);
        Assert.Equal(s_plugin, decisions.AutoLinker);
    }

    [Fact]
    public void ASourceFirstSeenWithoutALinkerIsClaimedByTheFirstLinkerLater()
    {
        var settings = new MetadataServiceSettings();
        MetadataProviderManager.SeedDecisions(settings, [Claim(s_core, TestSources.Plugin, links: false)], s_nothingCarried, out var none);
        Assert.Empty(none);
        Assert.Null(settings.Sources[TestSources.Plugin].AutoLinker);
        Assert.True(settings.Sources[TestSources.Plugin].AutoLinkerUnclaimed);

        var seeded = MetadataProviderManager.SeedDecisions(
            settings,
            [Claim(s_core, TestSources.Plugin, links: false), Claim(s_plugin, TestSources.Plugin), Claim(s_later, TestSources.Plugin)],
            s_nothingCarried,
            out var autoLinkers
        );

        Assert.True(seeded);
        Assert.Equal([(TestSources.Plugin, s_plugin.ToString()[..4])], autoLinkers);
        Assert.Equal(s_plugin, settings.Sources[TestSources.Plugin].AutoLinker);
        Assert.False(settings.Sources[TestSources.Plugin].AutoLinkerUnclaimed);
    }

    [Fact]
    public void OlderSettingsAreReadIn_AndOnlyWhatWasNeverDecidedIsFilled()
    {
        var settings = new MetadataServiceSettings
        {
            Sources =
            {
                [TestSources.Plugin] = new()
                {
                    // Turned off, and nobody set to auto-link.
                    Enabled = new() { [MetadataEntityType.Series] = null },
                    AutoLinker = null,
                    AutoLink = false,
                },
            },
        };

        MetadataProviderManager.SeedDecisions(settings, [Claim(s_plugin, TestSources.Plugin)], s_nothingCarried, out var autoLinkers);

        var decisions = settings.Sources[TestSources.Plugin];
        Assert.Equal([new(s_plugin, false)], decisions.Providers[MetadataEntityType.Series]);
        Assert.Equal([new(s_plugin, true)], decisions.Providers[MetadataEntityType.Episode]);
        Assert.Null(decisions.Enabled);
        Assert.Null(decisions.AutoLinker);
        Assert.False(decisions.AutoLink);
        Assert.Empty(autoLinkers);
    }

    [Fact]
    public void WhatAnUpgradeCarriedOverWinsOverTheDefault()
    {
        var settings = new MetadataServiceSettings();
        var carried = new Dictionary<MetadataSource, SettingsMigrations.AutoLinkCarryOver> { [TestSources.Plugin] = new(false, true) };

        MetadataProviderManager.SeedDecisions(settings, [Claim(s_plugin, TestSources.Plugin)], carried, out _);

        Assert.False(settings.Sources[TestSources.Plugin].AutoLink);
        Assert.True(settings.Sources[TestSources.Plugin].AutoLinkRestricted);
    }

    #endregion

    #region Persistence

    [Fact]
    public void TheClaimIsSaved_ALaterProviderLeavesItAlone_AndAStaleOneIsClaimedAgain()
    {
        var first = new FirstLinker();
        var boot = Boot();
        boot.AddParts([first]);
        var claimed = boot.GetProviderInfo(first);
        Assert.True(claimed.IsAutoLinker);
        Assert.True(claimed.AutoLink);
        Assert.Equal(claimed.AvailableEntityTypes.Order(), claimed.EnabledEntityTypes.Order());

        // Installed later on the same source, and registered ahead of it by
        // name, yet the source stays with the provider that claimed it.
        var later = new LaterLinker();
        boot = Boot();
        boot.AddParts([first, later]);
        Assert.Equal("Alpha", boot.MetadataProviders[0].Name);
        Assert.True(boot.GetProviderInfo(first).IsAutoLinker);
        Assert.False(boot.GetProviderInfo(later).IsAutoLinker);
        Assert.Empty(boot.GetProviderInfo(later).EnabledEntityTypes);

        // The claimant uninstalled, the one left claims the source again.
        boot = Boot();
        boot.AddParts([later]);
        var heir = boot.GetProviderInfo(later);
        Assert.True(heir.IsAutoLinker);
        Assert.Equal(heir.AvailableEntityTypes.Order(), heir.EnabledEntityTypes.Order());

        // And that is saved too.
        boot = Boot();
        boot.AddParts([first, later]);
        Assert.True(boot.GetProviderInfo(later).IsAutoLinker);
        Assert.False(boot.GetProviderInfo(first).IsAutoLinker);
    }

    [Fact]
    public void TurningAProviderOffSurvivesARestart()
    {
        var provider = new FirstLinker();
        var boot = Boot();
        boot.AddParts([provider]);
        var info = boot.GetProviderInfo(provider);
        boot.SetProviderEnabled(info.ID, false);
        boot.SetProviderAutoLinker(TestSources.Plugin, null);

        boot = Boot();
        boot.AddParts([provider]);

        Assert.False(boot.GetProviderInfo(provider).Enabled);
        Assert.False(boot.GetProviderInfo(provider).IsAutoLinker);
    }

    [Fact]
    public void ALinkerAddedLaterClaimsASourceNobodyCouldLink_UnlessAnAdminChose()
    {
        var refresher = new Refresher();
        var linker = new FirstLinker();
        var boot = Boot();
        boot.AddParts([refresher]);
        Assert.False(boot.GetProviderInfo(refresher).IsAutoLinker);

        // A later version adding auto-linking, or a new plugin, claims it,
        // in effect once it is also handed the series it links.
        boot = Boot();
        boot.AddParts([refresher, linker]);
        boot.SetProviderEnabled(boot.GetProviderInfo(linker).ID, true);
        Assert.True(boot.GetProviderInfo(linker).IsAutoLinker);

        // Choosing nobody while no linker exists is kept all the same.
        Directory.Delete(_dataPath, recursive: true);
        Directory.CreateDirectory(_dataPath);
        boot = Boot();
        boot.AddParts([refresher]);
        boot.SetProviderAutoLinker(TestSources.Plugin, null);

        boot = Boot();
        boot.AddParts([refresher, linker]);
        boot.SetProviderEnabled(boot.GetProviderInfo(linker).ID, true);
        Assert.False(boot.GetProviderInfo(linker).IsAutoLinker);
    }

    #endregion

    #region Order

    /// <summary>
    /// Boots with the first linker alone, then with the later one too, so the first claimed the source.
    /// </summary>
    private (MetadataProviderManager Boot, FirstLinker First, LaterLinker Later, Guid FirstID, Guid LaterID) BootFirstThenLater()
    {
        FirstLinker first = new();
        LaterLinker later = new();
        Boot().AddParts([first]);
        var boot = Boot();
        boot.AddParts([first, later]);
        return (boot, first, later, boot.GetProviderInfo(first).ID, boot.GetProviderInfo(later).ID);
    }

    [Fact]
    public void TheNextEnabledProviderTakesOverWhenTheOneAnsweringIsTurnedOffOrRemoved()
    {
        var (boot, first, later, firstID, laterID) = BootFirstThenLater();
        Assert.Equal([new(firstID, true), new(laterID, true)], boot.GetProviderOrder(TestSources.Plugin, MetadataEntityType.Series));

        boot.SetProviderEnabled(firstID, false);
        Assert.Empty(boot.GetProviderInfo(first).EnabledEntityTypes);
        Assert.Equal(boot.GetProviderInfo(later).AvailableEntityTypes.Order(), boot.GetProviderInfo(later).EnabledEntityTypes.Order());

        // Put back first for series alone, then removed: the one standing by answers again.
        boot.SetProviderOrder(TestSources.Plugin, new Dictionary<MetadataEntityType, IReadOnlyList<MetadataProviderAssignment>>
        {
            [MetadataEntityType.Series] = [new(firstID, true)],
        });
        Assert.Equal([MetadataEntityType.Series], boot.GetProviderInfo(first).EnabledEntityTypes);
        Assert.DoesNotContain(MetadataEntityType.Series, boot.GetProviderInfo(later).EnabledEntityTypes);
        boot = Boot();
        boot.AddParts([later]);
        Assert.Contains(MetadataEntityType.Series, boot.GetProviderInfo(later).EnabledEntityTypes);

        // Back again, it joins the end.
        boot = Boot();
        boot.AddParts([first, later]);
        Assert.Equal([new(laterID, true), new(firstID, true)], boot.GetProviderOrder(TestSources.Plugin, MetadataEntityType.Series));
    }

    [Fact]
    public void AnOrderIsSetForEachKindGivenAndRefusedWhole()
    {
        var (boot, first, later, firstID, laterID) = BootFirstThenLater();
        var before = boot.GetProviderOrder(TestSources.Plugin, MetadataEntityType.Series);

        var strangerKind = new Dictionary<MetadataEntityType, IReadOnlyList<MetadataProviderAssignment>>
        {
            [MetadataEntityType.Series] = [new(laterID, true)],
            [MetadataEntityType.Movie] = [new(laterID, true)],
        };
        var twice = new Dictionary<MetadataEntityType, IReadOnlyList<MetadataProviderAssignment>>
        {
            [MetadataEntityType.Series] = [new(laterID, true), new(laterID, false)],
        };
        Assert.Throws<ArgumentException>(() => boot.SetProviderOrder(TestSources.Plugin, strangerKind));
        Assert.Throws<ArgumentException>(() => boot.SetProviderOrder(TestSources.Plugin, twice));
        Assert.Equal(before, boot.GetProviderOrder(TestSources.Plugin, MetadataEntityType.Series));

        // One left out keeps its switch, after those given.
        boot.SetProviderOrder(TestSources.Plugin, new Dictionary<MetadataEntityType, IReadOnlyList<MetadataProviderAssignment>>
        {
            [MetadataEntityType.Series] = [new(laterID, false)],
            [MetadataEntityType.Episode] = [new(laterID, true), new(firstID, false)],
        });
        boot = Boot();
        boot.AddParts([first, later]);
        Assert.Equal([new(laterID, false), new(firstID, true)], boot.GetProviderOrder(TestSources.Plugin, MetadataEntityType.Series));
        Assert.Equal([MetadataEntityType.Series, MetadataEntityType.Season], boot.GetProviderInfo(first).EnabledEntityTypes.Order());
        Assert.Equal([MetadataEntityType.Episode], boot.GetProviderInfo(later).EnabledEntityTypes);
    }

    [Fact]
    public void TurningAProviderOnMovesItToTheFrontAndTurnsItOffForTheRest()
    {
        var (boot, first, later, firstID, laterID) = BootFirstThenLater();

        boot.SetProviderEnabled(laterID, new HashSet<MetadataEntityType> { MetadataEntityType.Series });

        Assert.Equal([new(laterID, true), new(firstID, true)], boot.GetProviderOrder(TestSources.Plugin, MetadataEntityType.Series));
        Assert.Equal([new(firstID, true), new(laterID, false)], boot.GetProviderOrder(TestSources.Plugin, MetadataEntityType.Episode));
    }

    #endregion
}
