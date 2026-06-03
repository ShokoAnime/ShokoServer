using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<ChainScopeRegistry> _logger;
    private readonly ConcurrentDictionary<Guid, IServiceScope> _scopes = new();

    public ChainScopeRegistry(IServiceScopeFactory scopeFactory, ILogger<ChainScopeRegistry> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public IServiceScope GetOrCreateChainScope(Guid chainId) =>
        _scopes.GetOrAdd(chainId, _ => _scopeFactory.CreateScope());

    public bool TryGetChainScope(Guid chainId, out IServiceScope scope) =>
        _scopes.TryGetValue(chainId, out scope!);

    public void CompleteChainScope(Guid chainId)
    {
        if (_scopes.TryRemove(chainId, out var scope))
        {
            scope.Dispose();
            _ = DeleteChainContextAsync(chainId);
        }
    }

    public void Dispose()
    {
        foreach (var scope in _scopes.Values)
            scope.Dispose();
        _scopes.Clear();
    }

    private async Task DeleteChainContextAsync(Guid chainId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IJobChainContextRepository>().DeleteAsync(chainId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete chain context {ChainId} from database", chainId);
        }
    }
}
