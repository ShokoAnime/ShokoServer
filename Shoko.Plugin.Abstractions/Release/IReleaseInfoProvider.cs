using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.Abstractions.Release;

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

/// <summary>
///   Indicates that the release information provider supports configuration,
///   and which configuration type to display in the UI.
/// </summary>
/// <typeparam name="TConfiguration">
///   The hash provider configuration type.
/// </typeparam>
public interface IReleaseInfoProvider<TConfiguration> : IReleaseInfoProvider where TConfiguration : IReleaseInfoProviderConfiguration { }
