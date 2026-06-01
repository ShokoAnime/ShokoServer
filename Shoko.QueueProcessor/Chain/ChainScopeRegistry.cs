using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Shoko.QueueProcessor.Chain;

/// <summary>
/// Singleton registry mapping <see cref="JobChainContext.ChainId"/> to the shared DI scope
/// that holds the chain's scoped services (including <see cref="JobChainContextAccessor"/>).
/// Chain scopes are created at chain-build time and disposed when the last job in the chain
/// completes or the chain is aborted with no remaining finally jobs.
/// </summary>
public sealed class ChainScopeRegistry : IChainScopeRegistry, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentDictionary<Guid, IServiceScope> _scopes = new();

    public ChainScopeRegistry(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public IServiceScope GetOrCreateChainScope(Guid chainId) =>
        _scopes.GetOrAdd(chainId, _ => _scopeFactory.CreateScope());

    public bool TryGetChainScope(Guid chainId, out IServiceScope scope) =>
        _scopes.TryGetValue(chainId, out scope!);

    public void CompleteChainScope(Guid chainId)
    {
        if (_scopes.TryRemove(chainId, out var scope))
            scope.Dispose();
    }

    public void Dispose()
    {
        foreach (var scope in _scopes.Values)
            scope.Dispose();
        _scopes.Clear();
    }
}
