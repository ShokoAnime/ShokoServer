using System;
using Shoko.Abstractions.Connectivity.Suspensions;

namespace Shoko.Server.Services;

/// <summary>
///   Reports one provider's suspensions into the <see cref="SuspensionService"/>.
///   Registered as an open generic singleton, so a plugin injects it for its
///   own provider.
/// </summary>
/// <typeparam name="TProvider">The provider the suspensions are for.</typeparam>
/// <param name="service">The service keeping the suspensions.</param>
public sealed class SuspensionReporter<TProvider>(SuspensionService service) : ISuspensionReporter<TProvider> where TProvider : ISuspensionProvider
{
    /// <inheritdoc />
    public SuspensionStatus Current
        => service.GetStatus(typeof(TProvider));

    /// <inheritdoc />
    public void Suspend(SuspensionKind kind, string? reason = null, DateTime? resumesAt = null, bool isLiftable = false)
        => service.Suspend(typeof(TProvider), kind, reason, resumesAt, isLiftable);

    /// <inheritdoc />
    public void Resume(SuspensionKind kind)
        => service.Resume(typeof(TProvider), kind);

    /// <inheritdoc />
    public void ResumeAll()
        => service.Resume(typeof(TProvider), null);
}
