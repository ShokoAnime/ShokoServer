using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Connectivity.Suspensions;
using Shoko.Plugin.Tmdb.Metadata;

namespace Shoko.Plugin.Tmdb.Api;

/// <summary>
///   The plugin's requests to TMDB, suspended while TMDB limits the rate or
///   answers with server errors. The rate limiter reports through it.
/// </summary>
public sealed class TmdbSuspensionProvider : ISuspensionProvider
{
    /// <inheritdoc />
    public string Name => "TMDB";

    /// <inheritdoc />
    public string? Description => "Requests to TMDB, held back while TMDB limits the rate or answers with server errors.";

    /// <inheritdoc />
    public IReadOnlyList<Type> HeldProviderTypes => [typeof(TmdbMetadataProvider)];

    /// <summary>
    ///   Never called: none of the plugin's suspensions can be lifted.
    /// </summary>
    /// <param name="kind">The kind of the suspension.</param>
    /// <param name="token">Cancels the lift.</param>
    /// <returns>A completed task.</returns>
    public Task Lift(SuspensionKind kind, CancellationToken token)
        => Task.CompletedTask;
}
