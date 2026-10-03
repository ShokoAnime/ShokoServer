using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Abstractions.ScheduledActions.Services;
using Shoko.Server.API;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// The legacy import queues its scheduled actions in order, and one refusing
/// or failing to queue leaves the rest queued.
/// </summary>
public class LegacyScheduledActionsTests
{
    #region Helpers

    /// <summary>
    /// Matches any scheduled action type in a setup.
    /// </summary>
    [TypeMatcher]
    private sealed class AnyAction : ITypeMatcher, IScheduledAction
    {
        public string Name => nameof(AnyAction);

        public bool Matches(Type typeArgument) => true;

        public Task Execute(IProgress<decimal> progress, CancellationToken token) => Task.CompletedTask;
    }

    private static ScheduledActionInfo Info(Type actionType) => new()
    {
        ID = Guid.NewGuid(),
        Name = actionType.Name,
        CategoryName = "Import",
        PluginID = Guid.Empty,
        Triggers = [],
        DefaultTriggers = [],
        HasCustomTriggers = false,
        LastRunAt = null,
        NextRunAt = null,
        State = ScheduledActionState.Idle,
        Progress = null,
        IsCancellable = false,
        JobKey = actionType.Name,
    };

    /// <summary>
    /// A scheduled action service knowing every action, which records the
    /// types it was asked to run, in order.
    /// </summary>
    /// <param name="invoked">Takes the type of each run asked for.</param>
    /// <param name="onInvoke">What a run of a type does.</param>
    /// <returns>The service.</returns>
    private static Mock<IScheduledActionService> Service(List<Type> invoked, Func<Type, ActionValidationResult?> onInvoke)
    {
        var infos = new Dictionary<Guid, Type>();
        var service = new Mock<IScheduledActionService>();
        service.Setup(each => each.GetScheduledAction<AnyAction>())
            .Returns(new InvocationFunc(invocation =>
            {
                var actionType = invocation.Method.GetGenericArguments()[0];
                var info = Info(actionType);
                infos[info.ID] = actionType;
                return info;
            }));
        service.Setup(each => each.InvokeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, CancellationToken _) =>
            {
                invoked.Add(infos[id]);
                return Task.FromResult(onInvoke(infos[id]));
            });
        return service;
    }

    private static List<Type> StepTypes()
        => [.. LegacyScheduledActions.ImportActionTypes];

    #endregion

    #region Tests

    [Fact]
    public async Task Import_QueuesEveryStepInOrder_PastARefusalAndAFailure()
    {
        var steps = StepTypes();
        var invoked = new List<Type>();
        var service = Service(invoked, actionType =>
            actionType == steps[1] ? new("Not now.")
            : actionType == steps[3] ? throw new InvalidOperationException("Broken.")
            : null);

        var queued = await LegacyScheduledActions.InvokeImport(service.Object, TestContext.Current.CancellationToken);

        Assert.Equal(steps, invoked);
        Assert.Equal(steps.Count - 2, queued);
    }

    [Fact]
    public async Task Import_StopsWhenCancelled()
    {
        var steps = StepTypes();
        var invoked = new List<Type>();
        var service = Service(invoked, actionType => actionType == steps[0] ? throw new OperationCanceledException() : null);

        await Assert.ThrowsAsync<OperationCanceledException>(() => LegacyScheduledActions.InvokeImport(service.Object, TestContext.Current.CancellationToken));

        Assert.Equal([steps[0]], invoked);
    }

    #endregion
}
