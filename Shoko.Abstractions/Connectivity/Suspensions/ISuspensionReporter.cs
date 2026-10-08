using System;

namespace Shoko.Abstractions.Connectivity.Suspensions;

/// <summary>
///   Reports the suspensions of one <see cref="ISuspensionProvider"/>. Inject
///   it into whatever knows about them, such as a rate limiter.
/// </summary>
/// <remarks>
///   Every member throws an <see cref="InvalidOperationException"/> when
///   <typeparamref name="TProvider"/> is not a loaded provider, which
///   includes any use before the providers are registered at start-up.
/// </remarks>
/// <typeparam name="TProvider">The provider the suspensions are for.</typeparam>
public interface ISuspensionReporter<TProvider> where TProvider : ISuspensionProvider
{
    /// <summary>
    ///   The provider's current status.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///   <typeparamref name="TProvider"/> is not a loaded provider.
    /// </exception>
    SuspensionStatus Current { get; }

    /// <summary>
    ///   Suspends the service for a reason, or updates the suspension of that
    ///   kind, keeping when it was first raised.
    /// </summary>
    /// <remarks>
    ///   A <paramref name="resumesAt"/> already in the past ignores the
    ///   report, the suspension being over.
    /// </remarks>
    /// <param name="kind">Why the service is suspended.</param>
    /// <param name="reason">A detail only the service knows, or <c>null</c>.</param>
    /// <param name="resumesAt">When the suspension ends, or <c>null</c> when it lasts until resumed.</param>
    /// <param name="isLiftable">Whether an admin may lift it early.</param>
    /// <exception cref="InvalidOperationException">
    ///   <typeparamref name="TProvider"/> is not a loaded provider.
    /// </exception>
    void Suspend(SuspensionKind kind, string? reason = null, DateTime? resumesAt = null, bool isLiftable = false);

    /// <summary>
    ///   Ends the suspension of a kind, if there is one.
    /// </summary>
    /// <param name="kind">The kind to end.</param>
    /// <exception cref="InvalidOperationException">
    ///   <typeparamref name="TProvider"/> is not a loaded provider.
    /// </exception>
    void Resume(SuspensionKind kind);

    /// <summary>
    ///   Ends every suspension of the provider.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///   <typeparamref name="TProvider"/> is not a loaded provider.
    /// </exception>
    void ResumeAll();
}
