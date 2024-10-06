using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Release;

namespace Shoko.Plugin.Abstractions.Services;

/// <summary>
/// Video service.
/// </summary>
public interface IVideoReleaseService
{
    /// <summary>
    /// Get the current release for the specified video, if one exists.
    /// </summary>
    /// <param name="video">The video to find a release for.</param>
    /// <returns>The found release, or <c>null</c> if none could be found.</returns>
    IReleaseInfo? GetCurrentReleaseForVideo(IVideo video);

    /// <summary>
    /// List out all available providers, if they're enabled for use in
    /// <see cref="FindReleaseForVideo(IVideo, bool, CancellationToken)"/> and their priority order
    /// when used in that method.
    /// </summary>
    /// <returns>An enumerable of <see cref="ReleaseInfoProviderInfo"/>s, one for each available <see cref="IReleaseInfoProvider"/>.</returns>
    IEnumerable<ReleaseInfoProviderInfo> GetAvailableProviders();

    /// <summary>
    /// Edit the settings for an <see cref="IReleaseInfoProvider"/>, such as
    /// whether it's enabled for automatic usage, and the priority during automatic usage.
    /// </summary>
    /// <param name="providers">The providers to update.</param>
    void UpdateProviders(params ReleaseInfoProviderInfo[] providers);

    /// <summary>
    /// Gets the <see cref="IReleaseInfoProvider"/> with the specified ID.
    /// </summary>
    /// <param name="providerID">The ID of the provider.</param>
    /// <returns>The provider, or <c>null</c> if none could be found.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="providerID"/> is <c>null</c>.</exception>
    IReleaseInfoProvider? GetProviderByID(string providerID);

    /// <summary>
    /// Asks all enabled <see cref="IReleaseInfoProvider"/>s, in priority order,
    /// to find a release until a release is found or all providers are
    /// exhausted.
    /// </summary>
    /// <remarks>
    /// This method does not save the found release to the database unless 
    /// <paramref name="saveToDb"/> is set to <c>true</c>.
    /// </remarks>
    /// <param name="video">The video to find a release for.</param>
    /// <param name="saveToDb">Whether or not to save the found release to the database.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The found release, or <c>null</c> if none could be found.</returns>
    Task<IReleaseInfo?> FindReleaseForVideo(IVideo video, bool saveToDb = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the release info for the specified video in the database, and
    /// returns the saved release info. This will overwrite any existing
    /// release for the video.
    /// </summary>
    /// <param name="video">The video to save the release for.</param>
    /// <param name="provider">The release info provider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The saved release, if the provider managed to find one.</returns>
    Task<IReleaseInfo?> SaveReleaseForVideo(IVideo video, IReleaseInfoProvider provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the release info for the specified video in the database, and
    /// returns the saved release info. This will overwrite any existing
    /// release for the video.
    /// </summary>
    /// <param name="video">The video to save the release for.</param>
    /// <param name="release">The release details to save.</param>
    /// <param name="providerName">The name of the provider.</param>
    /// <returns>The saved release.</returns>
    Task<IReleaseInfo> SaveReleaseForVideo(IVideo video, ReleaseInfo release, string providerName = "User");

    /// <summary>
    /// Clears the current release for the specified video.
    /// </summary>
    /// <param name="video">The video to clear the current release for.</param>
    /// <returns>A boolean indicating the successful removal of the release.</returns>
    Task<bool> ClearReleaseForVideo(IVideo video);
}
