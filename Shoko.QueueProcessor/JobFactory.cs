using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Orchestration;

namespace Shoko.QueueProcessor;

/// <summary>
/// Default implementation of <see cref="IJobFactory"/>. Mirrors the worker's job lifecycle:
/// DI resolve → configure → <see cref="IQueueJob.Setup"/> → <see cref="IQueueJob.PostInit"/>
/// → <see cref="IQueueJob.Process"/> within a single scoped lifetime.
/// </summary>
public sealed class JobFactory : IJobFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly QueueOrchestrator _orchestrator;

    public JobFactory(IServiceProvider serviceProvider, QueueOrchestrator orchestrator)
    {
        _serviceProvider = serviceProvider;
        _orchestrator = orchestrator;
    }

    public bool CanRun<T>() where T : class, IQueueJob
        => !_orchestrator.IsJobTypeBlocked(typeof(T));

    public async Task Execute<T>(Action<T>? configure = null) where T : class, IQueueJob
    {
        if (_orchestrator.IsJobTypeBlocked(typeof(T)))
            throw new JobBlockedException(typeof(T));

        using var scope = _serviceProvider.CreateScope();
        var instance = scope.ServiceProvider.GetRequiredService<T>();
        configure?.Invoke(instance);
        instance.Setup(scope.ServiceProvider);
        instance.PostInit();
        await instance.Process().ConfigureAwait(false);
    }
}
