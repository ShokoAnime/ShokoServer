using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Shoko.Server.API.SignalR.Aggregate;
using Shoko.Server.API.SignalR.Models;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Covers the queue feed's cancellation pushes.
/// </summary>
public class QueueEventEmitterTests
{
    #region Fixture

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

    private sealed class Harness : IDisposable
    {
        public QueueControllerTests.Fixture Queue { get; } = new();

        public ConcurrentQueue<QueueStateSignalRModel> Sent { get; } = new();

        public QueueEventEmitter Emitter { get; }

        public Harness()
        {
            var group = new Mock<IClientProxy>();
            group.Setup(proxy => proxy.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Callback((string _, object?[] args, CancellationToken _) => Sent.Enqueue((QueueStateSignalRModel)args[0]!))
                .Returns(Task.CompletedTask);
            var clients = new Mock<IHubClients>();
            clients.Setup(c => c.Group(It.IsAny<string>())).Returns(group.Object);
            var hub = new Mock<IHubContext<AggregateHub>>();
            hub.Setup(h => h.Clients).Returns(clients.Object);

            Emitter = new QueueEventEmitter(hub.Object, Queue.Events, Queue.Handler);
        }

        public void Dispose()
        {
            Emitter.Dispose();
            Queue.Dispose();
        }
    }

    #endregion

    #region Pushes

    [Fact]
    public async Task CancellationRequest_IsPushedWithTheRunningItem()
    {
        using var harness = new Harness();
        var key = await harness.Queue.Enqueue<QueueControllerTests.CancellableJob>(1, run: true);

        await harness.Queue.Scheduler.Cancel(key, TestContext.Current.CancellationToken);

        // Pushes from before the request carry the item without it, so wait for the one with it.
        var requested = () => harness.Sent.SelectMany(state => state.CurrentlyExecuting).FirstOrDefault(item => item.Key == key && item.IsCancellationRequested);
        using var cts = new CancellationTokenSource(_timeout);
        while (requested() is null)
            await Task.Delay(10, cts.Token);
        var pushed = requested()!;
        Assert.True(pushed.IsCancellable);
        Assert.Null(pushed.Progress);
    }

    #endregion
}
