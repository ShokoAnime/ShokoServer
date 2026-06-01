using System;
using Microsoft.Extensions.DependencyInjection;

namespace Shoko.QueueProcessor.Chain;

public interface IChainScopeRegistry
{
    IServiceScope GetOrCreateChainScope(Guid chainId);
    bool TryGetChainScope(Guid chainId, out IServiceScope scope);
    void CompleteChainScope(Guid chainId);
}
