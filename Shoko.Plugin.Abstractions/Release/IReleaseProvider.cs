using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.Abstractions.Release;

/// <summary>
/// Video release information provider.
/// </summary>
public interface IReleaseInfoProvider
{
    /// <summary>
    ///   Friendly name of the release information provider.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///   Version of the release information provider.
    /// </summary>
    Version Version { get; }

    /// <summary>
    ///   Gets the release information for the specified video, if available
    ///   from the provider.
    /// </summary>
    /// <param name="video">
    ///   The video.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. A cancellation token for cancelling the search.
    /// </param>
    /// <returns>
    ///   The release information, or <c>null</c> if not available.
    /// </returns>
    Task<ReleaseInfo?> GetReleaseInfoForVideo(IVideo video, CancellationToken cancellationToken);

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
}
