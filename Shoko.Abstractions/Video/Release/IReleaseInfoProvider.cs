using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Config;

namespace Shoko.Abstractions.Video.Release;

/// <summary>
///   Base interface for all video release information providers to implement.
/// </summary>
public interface IReleaseInfoProvider
{
    /// <summary>
    ///   Friendly name of the release information provider.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///   Optional. Description of the release information provider.
    /// </summary>
    string? Description { get => null; }

    /// <summary>
    ///   Version of the release information provider.
    /// </summary>
    Version Version { get => GetType().Assembly.GetName().Version ?? new Version(0, 0, 0, 0); }

    /// <summary>
    ///   Gets the release information for the specified video, if available
    ///   from the provider.
    /// </summary>
    /// <param name="context">
    ///   The release info request with the video to get release info for.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. A cancellation token for cancelling the search.
    /// </param>
    /// <returns>
    ///   The release information, or <c>null</c> if not available.
    /// </returns>
    Task<ReleaseInfo?> GetReleaseInfoForVideo(ReleaseInfoContext context, CancellationToken cancellationToken);

    /// <summary>
    ///   Gets the release information by a specified release id, if the
    ///   provider supports it.
    /// </summary>
    /// <param name="releaseId">
    ///   The release id.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. A cancellation token for cancelling the search.
    /// </param>
    /// <returns>
    ///   The release information, or <c>null</c> if not available.
    /// </returns>
    Task<ReleaseInfo?> GetReleaseInfoById(string releaseId, CancellationToken cancellationToken);

    /// <summary>
    ///   Called when a release was previously matched but the stored release
    ///   info may be incomplete. Returns the delay before this provider should
    ///   be called again to fill in missing fields, or <c>null</c> if no
    ///   further rescans should be scheduled — either because the info is
    ///   already complete, this provider did not originally match the file, or
    ///   the maximum number of rescan attempts has been reached.
    /// </summary>
    TimeSpan? GetRescanDelay(IReleaseInfo existingInfo, IReleaseMatchAttempt lastAttempt)
        => null;
}

/// <summary>
///   Indicates that the release information provider supports configuration,
///   and which configuration type to display in the UI.
/// </summary>
/// <typeparam name="TConfiguration">
///   The hash provider configuration type.
/// </typeparam>
public interface IReleaseInfoProvider<TConfiguration> : IReleaseInfoProvider where TConfiguration : IReleaseInfoProviderConfiguration { }
