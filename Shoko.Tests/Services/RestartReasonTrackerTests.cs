using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Namotion.Reflection;
using NJsonSchema;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Events;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Core.Events;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Server.Plugin;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

public class RestartReasonTrackerTests
{
    #region Configurations

    [Fact]
    public void ConfigurationReason_IsOneCoreReasonForEveryPendingConfiguration()
    {
        var harness = new Harness();
        var first = harness.AddConfiguration("Example Settings", harness.PluginA);
        var second = harness.AddConfiguration("Other Settings", harness.PluginB);

        harness.SetPending(first.ID, "Web.Port", "Database.Type");
        harness.SetPending(second.ID, "Sources");
        harness.Tracker.RefreshConfigurations();

        var reason = Assert.Single(harness.Tracker.Reasons);
        Assert.Equal(RestartReasonSource.Configuration, reason.Source);
        Assert.Equal(CorePlugin.StaticID, reason.PluginID);
        Assert.Equal(harness.Clock.Now, reason.RaisedAt);
        Assert.Single(harness.Events);
    }

    [Fact]
    public void ConfigurationReason_IsClearedWhenTheValuesGoBack()
    {
        var harness = new Harness();
        var configuration = harness.AddConfiguration("Example Settings", harness.PluginA);
        harness.SetPending(configuration.ID, "Web.Port");
        harness.Tracker.RefreshConfigurations();

        harness.SetPending(configuration.ID);
        harness.Tracker.RefreshConfigurations();

        Assert.Empty(harness.Tracker.Reasons);
        Assert.Equal(2, harness.Events.Count);
        Assert.False(harness.Events[^1].RestartRequired);
    }

    [Fact]
    public void ConfigurationReason_StandsUntilTheLastConfigurationGoesBack()
    {
        var harness = new Harness();
        var first = harness.AddConfiguration("Example Settings", harness.PluginA);
        var second = harness.AddConfiguration("Other Settings", harness.PluginB);
        harness.SetPending(first.ID, "Web.Port");
        harness.SetPending(second.ID, "Sources");
        var reason = Assert.Single(harness.Tracker.Reasons);

        harness.SetPending(first.ID);

        Assert.Same(reason, Assert.Single(harness.Tracker.Reasons));
        Assert.Single(harness.Events);

        harness.SetPending(second.ID);

        Assert.Empty(harness.Tracker.Reasons);
        Assert.Equal(2, harness.Events.Count);
    }

    [Fact]
    public void ConfigurationReason_IsKeptAsItIsWhenTheMembersChange()
    {
        var harness = new Harness();
        var configuration = harness.AddConfiguration("Example Settings", harness.PluginA);
        harness.SetPending(configuration.ID, "Web.Port");
        harness.Tracker.RefreshConfigurations();
        var reason = Assert.Single(harness.Tracker.Reasons);

        harness.Clock.Now = harness.Clock.Now.AddMinutes(5);
        harness.SetPending(configuration.ID, "Web.Port", "Web.Host");
        harness.Tracker.RefreshConfigurations();

        Assert.Same(reason, Assert.Single(harness.Tracker.Reasons));
        Assert.Single(harness.Events);
    }

    [Fact]
    public void ConfigurationReason_IsRaisedAgainWithANewTimeAfterItWasCleared()
    {
        var harness = new Harness();
        var configuration = harness.AddConfiguration("Example Settings", harness.PluginA);
        harness.SetPending(configuration.ID, "Web.Port");
        _ = harness.Tracker.Reasons;
        harness.SetPending(configuration.ID);
        _ = harness.Tracker.Reasons;

        harness.Clock.Now = harness.Clock.Now.AddMinutes(5);
        harness.SetPending(configuration.ID, "Web.Port");

        Assert.Equal(harness.Clock.Now, Assert.Single(harness.Tracker.Reasons).RaisedAt);
        Assert.Equal(3, harness.Events.Count);
    }

    [Fact]
    public void ConfigurationReason_RaisesNoEventWhenNothingChanged()
    {
        var harness = new Harness();
        var configuration = harness.AddConfiguration("Example Settings", harness.PluginA);
        harness.SetPending(configuration.ID, "Web.Port");

        harness.Tracker.RefreshConfigurations();
        harness.Tracker.RefreshConfigurations();
        _ = harness.Tracker.Reasons;

        Assert.Single(harness.Events);
    }

    [Fact]
    public void ConfigurationReason_FollowsTheConfigurationServiceEvents()
    {
        var harness = new Harness();
        var configuration = harness.AddConfiguration("Example Settings", harness.PluginA);

        harness.SetPending(configuration.ID, "Web.Port");
        harness.ConfigurationService.Raise(service => service.Saved += null, new ConfigurationSavedEventArgs { ConfigurationInfo = configuration });
        Assert.Single(harness.Events);

        harness.SetPending(configuration.ID);
        harness.ConfigurationService.Raise(service => service.RequiresRestart += null, new ConfigurationRequiresRestartEventArgs { RequiresRestart = false });
        Assert.Equal(2, harness.Events.Count);
        Assert.Empty(harness.Events[^1].Reasons);
    }

    [Fact]
    public void ConfigurationReason_SkipsConfigurationsTheServiceNoLongerKnows()
    {
        var harness = new Harness();

        harness.SetPending(Guid.NewGuid(), "Web.Port");

        Assert.Empty(harness.Tracker.Reasons);
        Assert.Empty(harness.Events);
    }

    [Fact]
    public async Task ConfigurationReason_ALateRefreshCannotBringBackAClearedReason()
    {
        var harness = new Harness();
        var configuration = harness.AddConfiguration("Example Settings", harness.PluginA);
        harness.SetPending(configuration.ID, "Web.Port");
        using var readStarted = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var stale = true;
        harness.ReadPending = pending =>
        {
            if (!stale)
                return pending;

            // The first refresh reads the pending members, then stalls until a
            // newer save has had its chance to clear them.
            stale = false;
            readStarted.Set();
            release.Wait(TimeSpan.FromSeconds(10));
            return pending;
        };

        var cancellationToken = TestContext.Current.CancellationToken;
        var older = Task.Run(harness.Tracker.RefreshConfigurations, cancellationToken);
        Assert.True(readStarted.Wait(TimeSpan.FromSeconds(10), cancellationToken));
        harness.SetPending(configuration.ID);

        // The newer refresh either waits on the tracker's lock until the older
        // one is done or, were the read outside it, runs to its end first.
        var newer = new Thread(harness.Tracker.RefreshConfigurations) { IsBackground = true };
        newer.Start();
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while ((newer.ThreadState & (ThreadState.WaitSleepJoin | ThreadState.Stopped)) is 0)
        {
            Assert.True(DateTime.UtcNow < deadline, "The newer refresh neither waited nor finished.");
            Thread.Yield();
        }

        release.Set();
        await older;
        Assert.True(newer.Join(TimeSpan.FromSeconds(10)));

        // Checked before reading the reasons, since a read catches up again.
        // The two refreshes may cancel out before either publishes.
        Assert.False(harness.Events.LastOrDefault()?.RestartRequired ?? false);
        Assert.Empty(harness.Tracker.Reasons);
    }

    [Fact]
    public void ConfigurationReason_LeavesOutTheServersEnabledPlugins()
    {
        var harness = new Harness();
        var configuration = harness.AddConfiguration("Server Settings", harness.PluginA, typeof(ServerSettings));

        harness.SetPending(configuration.ID, RestartReasonTracker.EnabledPluginsMember, "Web.Port");

        Assert.Equal(RestartReasonSource.Configuration, Assert.Single(harness.Tracker.Reasons).Source);

        harness.SetPending(configuration.ID, RestartReasonTracker.EnabledPluginsMember);

        Assert.Empty(harness.Tracker.Reasons);
        Assert.False(harness.Events[^1].RestartRequired);
    }

    [Fact]
    public void ConfigurationReason_KeepsTheEnabledPluginsOfOtherConfigurations()
    {
        var harness = new Harness();
        var configuration = harness.AddConfiguration("Example Settings", harness.PluginA);

        harness.SetPending(configuration.ID, RestartReasonTracker.EnabledPluginsMember);

        Assert.Equal(RestartReasonSource.Configuration, Assert.Single(harness.Tracker.Reasons).Source);
    }

    [Fact]
    public void EnablingAPlugin_RaisesExactlyOneReason()
    {
        var harness = new Harness();
        var configuration = harness.AddConfiguration("Server Settings", harness.PluginA, typeof(ServerSettings));
        var plugin = Plugin(1, active: false, enabled: false);
        harness.OtherPlugins.Add(plugin);
        harness.Tracker.StartTrackingPluginState();

        // What toggling a plugin does: its state and the saved server settings both change.
        plugin.IsEnabled = true;
        harness.SetPending(configuration.ID, RestartReasonTracker.EnabledPluginsMember);
        harness.Tracker.RefreshPluginState();
        harness.Tracker.RefreshConfigurations();

        var reason = Assert.Single(harness.Tracker.Reasons);
        Assert.Equal(RestartReasonSource.PluginState, reason.Source);
        Assert.Equal(CorePlugin.StaticID, reason.PluginID);
        Assert.Single(harness.Events);
    }

    [Fact]
    public void DisablingAPluginThatCannotLoad_RaisesNoReason()
    {
        var harness = new Harness();
        var configuration = harness.AddConfiguration("Server Settings", harness.PluginA, typeof(ServerSettings));
        var plugin = Plugin(1, active: false, enabled: true, canLoad: false);
        harness.OtherPlugins.Add(plugin);
        harness.Tracker.StartTrackingPluginState();

        plugin.IsEnabled = false;
        harness.SetPending(configuration.ID, RestartReasonTracker.EnabledPluginsMember);

        Assert.Empty(harness.Tracker.Reasons);
        Assert.Empty(harness.Events);
    }

    #endregion

    #region Plugin State

    [Fact]
    public void PluginState_IsNotTrackedUntilThePluginsAreInitialized()
    {
        var harness = new Harness();
        harness.PluginAInfo.IsEnabled = false;

        harness.Tracker.RefreshPluginState();

        Assert.Empty(harness.Tracker.Reasons);
    }

    [Fact]
    public void PluginState_RaisesOneCoreReasonAndClearsItWhenChangedBack()
    {
        var harness = new Harness();
        harness.Tracker.StartTrackingPluginState();
        Assert.Empty(harness.Tracker.Reasons);

        harness.PluginAInfo.IsEnabled = false;
        harness.Tracker.RefreshPluginState();

        var reason = Assert.Single(harness.Tracker.Reasons);
        Assert.Equal(RestartReasonSource.PluginState, reason.Source);
        Assert.Equal(CorePlugin.StaticID, reason.PluginID);
        Assert.Equal(harness.Clock.Now, reason.RaisedAt);

        harness.PluginAInfo.IsEnabled = true;
        harness.Tracker.RefreshPluginState();

        Assert.Empty(harness.Tracker.Reasons);
        Assert.Equal(2, harness.Events.Count);
    }

    [Fact]
    public void PluginState_IsOneReasonForEveryChangedPluginUntilTheLastIsChangedBack()
    {
        var harness = new Harness();
        harness.Tracker.StartTrackingPluginState();
        harness.PluginAInfo.IsEnabled = false;
        harness.Tracker.RefreshPluginState();
        var reason = Assert.Single(harness.Tracker.Reasons);

        harness.Clock.Now = harness.Clock.Now.AddMinutes(5);
        harness.PluginBInfo.IsEnabled = false;
        harness.Tracker.RefreshPluginState();
        harness.PluginAInfo.IsEnabled = true;
        harness.Tracker.RefreshPluginState();

        Assert.Same(reason, Assert.Single(harness.Tracker.Reasons));
        Assert.Single(harness.Events);

        harness.PluginBInfo.IsEnabled = true;
        harness.Tracker.RefreshPluginState();

        Assert.Empty(harness.Tracker.Reasons);
        Assert.Equal(2, harness.Events.Count);
    }

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(false, true, false, false)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, true, false)]
    [InlineData(true, true, false, true)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, false)]
    public void RestartPending_AgreesWithThePluginStateReason(bool active, bool enabled, bool canLoad, bool expected)
    {
        var plugin = Plugin(1, active, enabled, canLoad);

        Assert.Equal(expected, plugin.RestartPending);
        Assert.Equal(expected, RestartReasonTracker.HasPendingStateChange([plugin]));
    }

    [Fact]
    public void HasPendingStateChange_IsTrueForAnUninstalledPlugin()
    {
        var plugin = Plugin(1, active: true, enabled: false);
        plugin.UninstalledAt = DateTime.UtcNow;

        Assert.True(RestartReasonTracker.HasPendingStateChange([plugin]));
    }

    [Fact]
    public void HasPendingStateChange_IsTrueForAVersionSwitch()
        => Assert.True(RestartReasonTracker.HasPendingStateChange([Plugin(1, active: true, enabled: false), Plugin(2, active: false, enabled: true)]));

    [Fact]
    public void HasPendingStateChange_PrefersThePinnedVersionOverTheHighest()
    {
        var pinned = Plugin(1, active: true, enabled: true);
        pinned.IsPinned = true;

        Assert.False(RestartReasonTracker.HasPendingStateChange([pinned, Plugin(2, active: false, enabled: true)]));
    }

    [Fact]
    public void HasPendingStateChange_PicksTheHighestEnabledVersionWhenNoneIsPinned()
        => Assert.True(RestartReasonTracker.HasPendingStateChange([Plugin(1, active: true, enabled: true), Plugin(2, active: false, enabled: true)]));

    #endregion

    #region Plugin Reasons

    [Fact]
    public void Require_RaisesAReasonOwnedByThePlugin()
    {
        var harness = new Harness();

        var restart = harness.Tracker.Require(harness.PluginAInfo, "The enabled sources changed.");

        var reason = restart.Reason;
        Assert.True(restart.IsHeld);
        Assert.Same(reason, Assert.Single(harness.Tracker.Reasons));
        Assert.Equal(RestartReasonSource.Plugin, reason.Source);
        Assert.Equal(harness.PluginAInfo.ID, reason.PluginID);
        Assert.False(string.IsNullOrEmpty(reason.Key));
        Assert.Equal("The enabled sources changed.", reason.Description);
        Assert.Same(reason, Assert.Single(Assert.Single(harness.Events).Reasons));
    }

    [Fact]
    public void Require_RaisesAReasonOfItsOwnEachTime()
    {
        var harness = new Harness();

        var first = harness.Tracker.Require(harness.PluginAInfo, "The enabled sources changed.");
        var second = harness.Tracker.Require(harness.PluginAInfo, "The enabled sources changed.");

        Assert.NotEqual(first.Reason.Key, second.Reason.Key);
        Assert.Equal(2, harness.Tracker.Reasons.Count);
        Assert.Equal(2, harness.Events.Count);
    }

    [Fact]
    public void Dispose_ClearsTheReasonOnce()
    {
        var harness = new Harness();
        var restart = harness.Tracker.Require(harness.PluginAInfo, "The enabled sources changed.");

        restart.Dispose();
        restart.Dispose();

        Assert.False(restart.IsHeld);
        Assert.Empty(harness.Tracker.Reasons);
        Assert.Equal(2, harness.Events.Count);
        Assert.False(harness.Events[^1].RestartRequired);
    }

    [Fact]
    public void Dispose_OnlyClearsItsOwnReason()
    {
        var harness = new Harness();
        using var fromA = harness.Tracker.Require(harness.PluginAInfo, "Plugin A changed its sources.");
        using var alsoFromA = harness.Tracker.Require(harness.PluginAInfo, "Plugin A changed its kinds.");
        var fromB = harness.Tracker.Require(harness.PluginBInfo, "Plugin B changed its sources.");

        fromB.Dispose();
        alsoFromA.Dispose();

        var reason = Assert.Single(harness.Tracker.Reasons);
        Assert.Same(fromA.Reason, reason);
    }

    [Fact]
    public void Dispose_LeavesConfigurationAndPluginStateReasonsAlone()
    {
        var harness = new Harness();
        var configuration = harness.AddConfiguration("Example Settings", harness.PluginA);
        harness.SetPending(configuration.ID, "Web.Port");
        harness.Tracker.StartTrackingPluginState();
        harness.PluginAInfo.IsEnabled = false;
        harness.Tracker.RefreshPluginState();

        harness.Tracker.Require(harness.PluginAInfo, "A reason of the plugin's own.").Dispose();

        Assert.Equal(2, harness.Tracker.Reasons.Count);
        Assert.DoesNotContain(harness.Tracker.Reasons, reason => reason.Source is RestartReasonSource.Plugin);
    }

    [Fact]
    public void Require_RefusesAPluginThatIsNotActive()
    {
        var harness = new Harness();
        var inactive = PluginTestDoubles.InactivePluginInfo(harness.PluginAInfo);

        Assert.Throws<ArgumentException>(() => harness.Tracker.Require(inactive, "Changed."));
        Assert.Throws<ArgumentNullException>(() => harness.Tracker.Require(null!, "Changed."));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Require_RefusesAnEmptyDescription(string description)
    {
        var harness = new Harness();

        Assert.Throws<ArgumentException>(() => harness.Tracker.Require(harness.PluginAInfo, description));
        Assert.Empty(harness.Tracker.Reasons);
    }

    #endregion

    #region Aggregation

    [Fact]
    public void Reasons_AreTheUnionOfEverySourceOldestFirst()
    {
        var harness = new Harness();
        harness.Tracker.StartTrackingPluginState();
        harness.Tracker.Require(harness.PluginAInfo, "Changed.");

        harness.Clock.Now = harness.Clock.Now.AddMinutes(1);
        harness.PluginBInfo.IsEnabled = false;
        harness.Tracker.RefreshPluginState();

        harness.Clock.Now = harness.Clock.Now.AddMinutes(1);
        var configuration = harness.AddConfiguration("Example Settings", harness.PluginA);
        harness.SetPending(configuration.ID, "Web.Port");

        var reasons = harness.Tracker.Reasons;

        Assert.Equal([RestartReasonSource.Plugin, RestartReasonSource.PluginState, RestartReasonSource.Configuration], reasons.Select(reason => reason.Source));
        Assert.Equal(reasons, harness.Events[^1].Reasons);
    }

    [Fact]
    public void Changed_KeepsTellingTheOtherHandlersWhenOneThrows()
    {
        var harness = new Harness();
        var told = 0;
        harness.Tracker.Changed += (_, _) => throw new InvalidOperationException("Broken handler.");
        harness.Tracker.Changed += (_, _) => told++;

        harness.Tracker.Require(harness.PluginAInfo, "Changed.");

        Assert.Equal(1, told);
        Assert.Single(harness.Events);
    }

    [Fact]
    public void Changed_TellsEveryHandlerAboutAChangeAHandlerMadeAfterTheOneBeforeIt()
    {
        var harness = new Harness();
        var lateEvents = new List<RestartReasonsChangedEventArgs>();
        harness.Tracker.Changed += (_, eventArgs) =>
        {
            if (eventArgs.Reasons.Count is 1)
                harness.Tracker.Require(harness.PluginBInfo, "Raised from a handler.");
        };
        harness.Tracker.Changed += (_, eventArgs) => lateEvents.Add(eventArgs);

        harness.Tracker.Require(harness.PluginAInfo, "Changed.");

        Assert.Equal([1, 2], harness.Events.Select(eventArgs => eventArgs.Reasons.Count));
        Assert.Equal([1, 2], lateEvents.Select(eventArgs => eventArgs.Reasons.Count));
        Assert.Equal(2, harness.Tracker.Reasons.Count);
    }

    #endregion

    #region Helpers

    private static LocalPluginInfo Plugin(int major, bool active, bool enabled, bool canLoad = true)
    {
        var template = PluginTestDoubles.InstalledPluginInfo(typeof(PluginTestDoubles.TestPlugin), Guid.Parse("7d0e7b5e-4a36-4d7e-9c55-0f1c3a1c9f00"));
        var plugin = new LocalPluginInfo
        {
            ID = template.ID,
            Name = template.Name,
            Description = template.Description,
            Version = new()
            {
                Version = new Version(major, 0, 0, 0),
                RuntimeIdentifier = template.Version.RuntimeIdentifier,
                AbstractionVersion = template.Version.AbstractionVersion,
                SourceRevision = null,
                ReleaseTag = null,
                Channel = template.Version.Channel,
                ReleasedAt = template.Version.ReleasedAt,
            },
            Authors = null,
            RepositoryUrl = null,
            HomepageUrl = null,
            Tags = [],
            Thumbnail = null,
            Icon = null,
            InstalledAt = DateTime.UnixEpoch,
            IsEnabled = enabled,
            IsActive = active,
            CanLoad = canLoad,
            CanUninstall = true,
            Plugin = active ? new PluginTestDoubles.TestPlugin() : null,
            PluginType = active ? typeof(PluginTestDoubles.TestPlugin) : null,
            ServiceRegistrationType = null,
            ApplicationRegistrationType = null,
            ContainingDirectory = null,
            DLLs = [$"SomePlugin.{major}.dll"],
            Types = [],
            Dependencies = [],
        };
        return plugin;
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTime Now { get; set; } = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

        public override DateTimeOffset GetUtcNow() => new(Now);
    }

    private sealed class Harness
    {
        private readonly Dictionary<Guid, IReadOnlySet<string>> _pending = [];

        private readonly Dictionary<Guid, ConfigurationInfo> _configurations = [];

        public Mock<IConfigurationService> ConfigurationService { get; } = new();

        public Mock<IPluginManager> PluginManager { get; } = new();

        public TestClock Clock { get; } = new();

        public PluginTestDoubles.TestPlugin PluginA { get; } = new();

        public PluginTestDoubles.TestPlugin PluginB { get; } = new();

        public LocalPluginInfo PluginAInfo { get; }

        public LocalPluginInfo PluginBInfo { get; }

        public RestartReasonTracker Tracker { get; }

        public List<RestartReasonsChangedEventArgs> Events { get; } = [];

        public List<LocalPluginInfo> OtherPlugins { get; } = [];

        public Func<IReadOnlyDictionary<Guid, IReadOnlySet<string>>, IReadOnlyDictionary<Guid, IReadOnlySet<string>>>? ReadPending { get; set; }

        public Harness()
        {
            PluginAInfo = PluginTestDoubles.InstalledPluginInfo(typeof(PluginTestDoubles.TestPlugin), PluginA.ID, "PluginA.dll", PluginA);
            PluginBInfo = PluginTestDoubles.InstalledPluginInfo(typeof(PluginTestDoubles.TestPlugin), PluginB.ID, "PluginB.dll", PluginB);
            PluginAInfo.CanLoad = true;
            PluginBInfo.CanLoad = true;
            PluginManager.Setup(manager => manager.GetPluginInfos()).Returns(() => [PluginAInfo, PluginBInfo, .. OtherPlugins]);
            PluginManager.Setup(manager => manager.GetPluginInfo(It.IsAny<IPlugin>()))
                .Returns((IPlugin plugin) => new[] { PluginAInfo, PluginBInfo }.FirstOrDefault(info => ReferenceEquals(info.Plugin, plugin)));
            ConfigurationService.Setup(service => service.RestartPendingFor).Returns(() => ReadPending?.Invoke(_pending.ToDictionary()) ?? _pending.ToDictionary());
            ConfigurationService.Setup(service => service.GetConfigurationInfo(It.IsAny<Guid>()))
                .Returns((Guid id) => _configurations.GetValueOrDefault(id));

            Tracker = new(NullLogger.Instance, ConfigurationService.Object, PluginManager.Object, Clock);
            Tracker.Changed += (_, eventArgs) => Events.Add(eventArgs);
        }

        public ConfigurationInfo AddConfiguration(string name, IPlugin owner, Type? type = null)
        {
            var info = new ConfigurationInfo(ConfigurationService.Object)
            {
                ID = Guid.NewGuid(),
                Path = null,
                Name = name,
                Description = string.Empty,
                HasCustomActions = false,
                HasCustomNewFactory = false,
                HasCustomValidation = false,
                HasCustomSave = false,
                HasCustomLoad = false,
                HasLiveEdit = false,
                Type = type ?? typeof(object),
                ContextualType = (type ?? typeof(object)).ToContextualType(),
                Schema = new JsonSchema(),
                PluginInfo = ReferenceEquals(owner, PluginA) ? PluginAInfo : PluginBInfo,
            };
            _configurations[info.ID] = info;
            return info;
        }

        public void SetPending(Guid configurationID, params string[] members)
        {
            if (members.Length is 0)
                _pending.Remove(configurationID);
            else
                _pending[configurationID] = members.ToHashSet();
        }
    }

    #endregion
}
