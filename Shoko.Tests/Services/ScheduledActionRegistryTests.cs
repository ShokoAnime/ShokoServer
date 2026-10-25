using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Abstractions.Utilities;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Builder;
using Shoko.Server.Actions;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers the scheduled action registry: the load-time checks on what a
/// scheduled action declares, the job a run queues and its key, and what the
/// core's scheduled actions may not declare.
/// </summary>
public class ScheduledActionRegistryTests
{
    #region Test Actions

    public sealed class PlainAction : IScheduledAction
    {
        public string Name => "Plain";

        public IReadOnlyList<ActionTrigger> DefaultTriggers => [ActionTrigger.Every(TimeSpan.FromHours(2))];

        public Task Execute(IProgress<decimal> progress, CancellationToken token) => Task.CompletedTask;
    }

    public sealed class RefusingAction : IScheduledAction
    {
        public string Name => "Refusing";

        public Task<ActionValidationResult?> Validate(CancellationToken token)
            => Task.FromResult<ActionValidationResult?>(new("Never."));

        public Task Execute(IProgress<decimal> progress, CancellationToken token) => Task.CompletedTask;
    }

    public sealed class BothKindsAction : IScheduledAction, IExecutableAction
    {
        public string Name => "Both Kinds";

        public ActionPermission Permission => ActionPermission.Admin;

        public Task Execute(CancellationToken token = default) => Task.CompletedTask;

        public Task Execute(IProgress<decimal> progress, CancellationToken token) => Task.CompletedTask;
    }

    public sealed class InvalidTriggerAction : IScheduledAction
    {
        public string Name => "Invalid Trigger";

        public IReadOnlyList<ActionTrigger> DefaultTriggers => [new ActionTrigger { Type = ActionTriggerType.Daily }];

        public Task Execute(IProgress<decimal> progress, CancellationToken token) => Task.CompletedTask;
    }

    public sealed class RateLimitedAction : IScheduledAction
    {
        public string Name => "Rate Limited";

        public IReadOnlyList<ActionTrigger> DefaultTriggers => [ActionTrigger.Every(TimeSpan.FromHours(6))];

        public TimeSpan? MinimumInterval => TimeSpan.FromHours(6);

        public Task Execute(IProgress<decimal> progress, CancellationToken token) => Task.CompletedTask;
    }

    public sealed class DefaultUnderItsMinimumAction : IScheduledAction
    {
        public string Name => "Default Under Its Minimum";

        public IReadOnlyList<ActionTrigger> DefaultTriggers => [ActionTrigger.Every(TimeSpan.FromHours(1))];

        public TimeSpan? MinimumInterval => TimeSpan.FromHours(6);

        public Task Execute(IProgress<decimal> progress, CancellationToken token) => Task.CompletedTask;
    }

    public sealed class DefaultsCloserThanItsMinimumAction : IScheduledAction
    {
        public string Name => "Defaults Closer Than Its Minimum";

        public IReadOnlyList<ActionTrigger> DefaultTriggers => [ActionTrigger.DailyAt(new(3, 0)), ActionTrigger.DailyAt(new(4, 0))];

        public TimeSpan? MinimumInterval => TimeSpan.FromHours(6);

        public Task Execute(IProgress<decimal> progress, CancellationToken token) => Task.CompletedTask;
    }

    public sealed class SecondsMinimumAction : IScheduledAction
    {
        public string Name => "Seconds Minimum";

        public TimeSpan? MinimumInterval => TimeSpan.FromSeconds(90);

        public Task Execute(IProgress<decimal> progress, CancellationToken token) => Task.CompletedTask;
    }

    public sealed class TooShortMinimumAction : IScheduledAction
    {
        public string Name => "Too Short Minimum";

        public TimeSpan? MinimumInterval => TimeSpan.Zero;

        public Task Execute(IProgress<decimal> progress, CancellationToken token) => Task.CompletedTask;
    }

    [JobPriority(Default = QueuePriority.Prioritized, Prioritized = QueuePriority.Prioritized + 10)]
    public sealed class RankedJob : IQueueJob
    {
        public string TypeName => nameof(RankedJob);

        public string Title => string.Empty;

        public Dictionary<string, object> Details => [];

        public void PostInit() { }

        public Task Process() => Task.CompletedTask;
    }

    public sealed class RankedJobAction(IQueueScheduler scheduler) : QueueJobScheduledAction<RankedJob>(scheduler)
    {
        public override string Name => "Ranked Job";
    }

    #endregion

    #region Fixture

    private static readonly Guid PluginID = Guid.NewGuid();

    private sealed class Harness
    {
        public Mock<IQueueScheduler> Scheduler { get; } = new();

        public ScheduledActionRegistry Registry { get; }

        public Harness(params Type[] actionTypes)
        {
            var services = new ServiceCollection();
            services.AddSingleton(Scheduler.Object);
            foreach (var type in actionTypes)
                services.AddTransient(type);
            Registry = new(Scheduler.Object, Mock.Of<IPluginManager>(), services.BuildServiceProvider(), NullLogger<ScheduledActionRegistry>.Instance);
            Registry.AddParts(actionTypes.Select(type => (PluginID, type)));
        }

        public Guid IDOf<T>() where T : class, IScheduledAction
            => Registry.GetAction(typeof(T))!.ID;
    }

    #endregion

    #region Registration

    [Fact]
    public void RegisteredScheduledActions_AreListed_UnderTheirPlugin()
    {
        var harness = new Harness(typeof(PlainAction), typeof(CheckNetworkAvailabilityAction));

        Assert.Equal(
            new[] { harness.IDOf<CheckNetworkAvailabilityAction>(), harness.IDOf<PlainAction>() }.Order(),
            harness.Registry.GetActions().Select(action => action.ID).Order()
        );
        Assert.Equal(PluginID, harness.Registry.GetAction(harness.IDOf<PlainAction>())!.PluginID);
        Assert.Null(harness.Registry.GetAction(typeof(RefusingAction)));
    }

    [Theory]
    [InlineData(typeof(BothKindsAction))]
    [InlineData(typeof(InvalidTriggerAction))]
    [InlineData(typeof(DefaultUnderItsMinimumAction))]
    [InlineData(typeof(DefaultsCloserThanItsMinimumAction))]
    [InlineData(typeof(SecondsMinimumAction))]
    [InlineData(typeof(TooShortMinimumAction))]
    public void WhatAScheduledActionCannotDeclare_FailsAtLoad(Type actionType)
        => Assert.Throws<InvalidOperationException>(() => new Harness(actionType));

    [Fact]
    public void AScheduledActionDeclaringNoMinimum_GetsAMinute()
    {
        var harness = new Harness(typeof(PlainAction), typeof(RateLimitedAction));

        Assert.Equal(TimeSpan.FromMinutes(1), harness.Registry.GetAction(harness.IDOf<PlainAction>())!.MinimumInterval);
        Assert.Equal(TimeSpan.FromHours(6), harness.Registry.GetAction(harness.IDOf<RateLimitedAction>())!.MinimumInterval);
    }

    #endregion

    #region Running

    [Fact]
    public async Task ARun_QueuesTheScheduledActionJob_UnderTheRunKey()
    {
        var harness = new Harness(typeof(PlainAction));
        var id = harness.IDOf<PlainAction>();
        Action<ScheduledActionJob>? configure = null;
        int? priority = null;
        harness.Scheduler
            .Setup(scheduler => scheduler.EnqueueWithPriority(It.IsAny<Action<ScheduledActionJob>?>(), It.IsAny<int>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .Callback<Action<ScheduledActionJob>?, int, DateTimeOffset?, CancellationToken>((job, jobPriority, _, _) => (configure, priority) = (job, jobPriority))
            .Returns(Task.CompletedTask);

        Assert.Null(await harness.Registry.InvokeAsync(id, TestContext.Current.CancellationToken));

        Assert.NotNull(configure);
        Assert.Equal(QueuePriority.ScheduledFor(typeof(ScheduledActionJob), false), priority);
        var key = JobKeyBuilder<ScheduledActionJob>.Create().UsingJobData(configure).Build();
        Assert.Equal(harness.Registry.GetAction(id)!.JobKey, key);
    }

    [Fact]
    public async Task ARun_OfAJobAction_QueuesTheJobItself_UnderItsOwnKey()
    {
        var harness = new Harness(typeof(CheckNetworkAvailabilityAction));
        var id = harness.IDOf<CheckNetworkAvailabilityAction>();
        harness.Scheduler
            .Setup(scheduler => scheduler.EnqueueWithPriority(It.IsAny<Action<CheckNetworkAvailabilityJob>?>(), It.IsAny<int>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Assert.Null(await harness.Registry.InvokeAsync(id, TestContext.Current.CancellationToken));

        harness.Scheduler.Verify(
            scheduler => scheduler.EnqueueWithPriority(
                It.IsAny<Action<CheckNetworkAvailabilityJob>?>(),
                QueuePriority.Scheduled,
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
        harness.Scheduler.Verify(
            scheduler => scheduler.EnqueueWithPriority(
                It.IsAny<Action<ScheduledActionJob>?>(),
                It.IsAny<int>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Never
        );
        Assert.Equal(JobKeyBuilder<CheckNetworkAvailabilityJob>.Create().Build(), harness.Registry.GetAction(id)!.JobKey);
    }

    [Fact]
    public async Task ARun_OfAJobAction_TakesTheJobTypesDefaultWhenAboveScheduled()
    {
        var scheduler = new Mock<IQueueScheduler>();

        await new RankedJobAction(scheduler.Object).EnqueueJob(TestContext.Current.CancellationToken);

        scheduler.Verify(
            queue => queue.EnqueueWithPriority(
                It.IsAny<Action<RankedJob>?>(),
                QueuePriority.For(typeof(RankedJob), prioritize: false),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task ARun_TheScheduledActionRefuses_QueuesNothing()
    {
        var harness = new Harness(typeof(RefusingAction));

        Assert.Equal("Never.", (await harness.Registry.InvokeAsync(harness.IDOf<RefusingAction>(), TestContext.Current.CancellationToken))?.Reason);

        Assert.Empty(harness.Scheduler.Invocations);
    }

    [Fact]
    public async Task ARunOfAnUnknownID_Throws()
        => await Assert.ThrowsAsync<KeyNotFoundException>(() => new Harness().Registry.InvokeAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));

    #endregion

    #region Core Scheduled Actions

    [Fact]
    public void NoCoreScheduledAction_HasAPublicSettableProperty()
    {
        // A scheduled action takes no parameters; a run with options is an
        // executable action's job.
        var settable = typeof(ScheduledActionRegistry).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false, ContainsGenericParameters: false })
            .Where(type => typeof(IScheduledAction).IsAssignableFrom(type))
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance), (type, property) => (type, property))
            .Where(pair => pair.property.SetMethod is { IsPublic: true })
            .Select(pair => $"{pair.type.FullName}.{pair.property.Name}")
            .ToList();

        Assert.Empty(settable);
    }

    #endregion

    #region Providers

    /// <summary>
    /// A provider whose actions the test changes.
    /// </summary>
    private sealed class FakeProvider : IScheduledActionProvider
    {
        public List<ScheduledActionDescriptor> Actions { get; } = [];

        public List<string> Executed { get; } = [];

        public event EventHandler? ActionsChanged;

        public IReadOnlyList<ScheduledActionDescriptor> GetActions() => [.. Actions];

        public Task Execute(string key, IProgress<decimal> progress, CancellationToken token)
        {
            Executed.Add(key);
            return Task.CompletedTask;
        }

        public void Change() => ActionsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static ScheduledActionDescriptor Descriptor(string key, string? name = null) => new() { Key = key, Name = name ?? key };

    [Fact]
    public async Task AProvidersActions_AreListedUnderIDsOfTheProviderAndKey_AndRunThroughIt()
    {
        var harness = new Harness();
        var provider = new FakeProvider();
        provider.Actions.Add(Descriptor("nightly-scan"));

        harness.Registry.AddProviders([provider]);

        var id = UuidUtility.GetV5($"ScheduledActionProvider={typeof(FakeProvider).FullName},Action=nightly-scan", Guid.Empty);
        Assert.Equal(id, Assert.Single(harness.Registry.GetActions()).ID);
        Assert.Same(provider, Assert.Single(harness.Registry.Providers));
        await harness.Registry.CreateInstance(id).Execute(new Progress<decimal>(), TestContext.Current.CancellationToken);
        Assert.Equal(["nightly-scan"], provider.Executed);
    }

    [Fact]
    public void AProviderChangingItsActions_AddsUpdatesAndRemovesThem_AndSaysSo()
    {
        var harness = new Harness();
        var provider = new FakeProvider();
        provider.Actions.AddRange([Descriptor("kept"), Descriptor("renamed"), Descriptor("dropped")]);
        harness.Registry.AddProviders([provider]);
        ScheduledActionSourceChangedEventArgs? changed = null;
        harness.Registry.Changed += (_, e) => changed = e;

        provider.Actions.Clear();
        provider.Actions.AddRange([Descriptor("kept"), Descriptor("renamed", "Renamed"), Descriptor("new")]);
        provider.Change();

        Assert.NotNull(changed);
        Assert.Equal(["new"], changed.Added.Select(action => action.Name));
        Assert.Equal(["Renamed"], changed.Updated.Select(action => action.Name));
        Assert.Equal(["dropped"], changed.Removed.Select(action => action.Name));
        Assert.Equal(["Renamed", "kept", "new"], harness.Registry.GetActions().Select(action => action.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AnInvalidKey_FailsTheStart_ButIsOnlyLeftOutLater()
    {
        var provider = new FakeProvider();
        provider.Actions.Add(Descriptor("Not Kebab"));
        Assert.Throws<InvalidOperationException>(() => new Harness().Registry.AddProviders([provider]));

        var harness = new Harness();
        var later = new FakeProvider();
        later.Actions.Add(Descriptor("fine"));
        harness.Registry.AddProviders([later]);
        later.Actions.AddRange([Descriptor("Not Kebab"), Descriptor("fine")]);
        later.Change();

        Assert.Equal(["fine"], harness.Registry.GetActions().Select(action => action.Name));
    }

    #endregion
}
