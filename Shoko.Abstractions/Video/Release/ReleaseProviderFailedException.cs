using System;

namespace Shoko.Abstractions.Video.Release;

/// <summary>
///   Thrown when a release search stops because a provider failed while it
///   searched: it threw, and was not one suspended while searching with
///   another provider left to ask. The provider's own exception is the
///   <see cref="Exception.InnerException"/>.
/// </summary>
/// <param name="providerID">The ID of the provider that failed.</param>
/// <param name="providerName">The name of the provider that failed.</param>
/// <param name="innerException">What the provider threw.</param>
public class ReleaseProviderFailedException(Guid providerID, string providerName, Exception innerException)
    : InvalidOperationException($"The release provider \"{providerName}\" failed while searching: {innerException.Message}", innerException)
{
    /// <summary>
    ///   The ID of the provider that failed.
    /// </summary>
    public Guid ProviderID { get; } = providerID;

    /// <summary>
    ///   The name of the provider that failed.
    /// </summary>
    public string ProviderName { get; } = providerName;
}
