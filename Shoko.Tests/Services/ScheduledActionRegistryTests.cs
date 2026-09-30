using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.ScheduledActions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Builder;
using Shoko.Server.Actions;
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
            Registry = new(Scheduler.Object, Mock.Of<IPluginManager>(), services.BuildServiceProvider());
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
        harness.Scheduler
            .Setup(scheduler => scheduler.Enqueue(It.IsAny<Action<ScheduledActionJob>?>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .Callback<Action<ScheduledActionJob>?, bool, DateTimeOffset?, CancellationToken>((job, _, _, _) => configure = job)
            .Returns(Task.CompletedTask);

        Assert.Null(await harness.Registry.InvokeAsync(id, TestContext.Current.CancellationToken));

        Assert.NotNull(configure);
        var key = JobKeyBuilder<ScheduledActionJob>.Create().UsingJobData(configure).Build();
        Assert.Equal(harness.Registry.GetAction(id)!.JobKey, key);
    }

    [Fact]
    public async Task ARun_OfAJobAction_QueuesTheJobItself_UnderItsOwnKey()
    {
        var harness = new Harness(typeof(CheckNetworkAvailabilityAction));
        var id = harness.IDOf<CheckNetworkAvailabilityAction>();
        harness.Scheduler
            .Setup(scheduler => scheduler.Enqueue(It.IsAny<Action<CheckNetworkAvailabilityJob>?>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Assert.Null(await harness.Registry.InvokeAsync(id, TestContext.Current.CancellationToken));

        harness.Scheduler.Verify(scheduler => scheduler.Enqueue(It.IsAny<Action<CheckNetworkAvailabilityJob>?>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Once);
        harness.Scheduler.Verify(scheduler => scheduler.Enqueue(It.IsAny<Action<ScheduledActionJob>?>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(JobKeyBuilder<CheckNetworkAvailabilityJob>.Create().Build(), harness.Registry.GetAction(id)!.JobKey);
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
}
