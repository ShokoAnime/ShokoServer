using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Shoko.Abstractions.Connectivity.Suspensions;

/// <summary>
///   One suspension status a plugin keeps for a service it talks to, such as
///   a remote API. Found like every other provider; report through
///   <see cref="ISuspensionReporter{TProvider}"/>.
/// </summary>
/// <remarks>
///   One class is one status: a plugin talking to a service over two
///   channels with their own limits exports one provider per channel.
/// </remarks>
public interface ISuspensionProvider
{
    /// <summary>
    ///   The display name of the service or channel, such as
    ///   <c>AniDB UDP</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///   Describes what is suspended, or <c>null</c>.
    /// </summary>
    string? Description { get; }

    /// <summary>
    ///   The types of the metadata and release providers whose queued jobs
    ///   wait while this provider is suspended. Read once, at registration.
    /// </summary>
    IReadOnlyList<Type> HeldProviderTypes { get; }

    /// <summary>
    ///   Ends a suspension early, for an admin. Called only for a suspension
    ///   reported as liftable; the core removes it once this returns.
    /// </summary>
    /// <param name="kind">The kind of the suspension to lift.</param>
    /// <param name="token">Cancels the lift.</param>
    /// <returns>A task that completes once the service may be used again.</returns>
    Task Lift(SuspensionKind kind, CancellationToken token);
}
