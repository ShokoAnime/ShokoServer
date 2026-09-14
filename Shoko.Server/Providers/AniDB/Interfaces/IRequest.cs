using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Shoko.Server.Providers.AniDB.Interfaces;

public interface IRequest<TResponse> where TResponse : class
{
    Task<TResponse> SendAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Marker interface used to discover concrete request types for DI registration.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public interface IRequest
{
}
