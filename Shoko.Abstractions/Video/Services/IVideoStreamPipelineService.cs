using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.Abstractions.Video.Streaming;

namespace Shoko.Abstractions.Video.Services;

/// <summary>
///   Service responsible for managing video stream transforms and playback
///   observers — the plugin extension points for pre-processing (e.g.
///   transcoding) and observing (e.g. scrobbling) video stream requests.
/// </summary>
public interface IVideoStreamPipelineService
{
    /// <summary>
    ///   Event raised when the enabled/priority state of video stream transforms is updated.
    /// </summary>
    event EventHandler? TransformsUpdated;

    /// <summary>
    ///   Event raised when the enabled state of playback observers is updated.
    /// </summary>
    event EventHandler? ObserversUpdated;

    /// <summary>
    ///   Adds the video stream transforms. This should be called once per
    ///   instance of the service, and will be called during start-up. Calling
    ///   it multiple times will have no effect.
    /// </summary>
    /// <param name="transforms">The video stream transforms.</param>
    void AddTransformParts(IEnumerable<IVideoStreamTransform> transforms);

    /// <summary>
    ///   Adds the playback observers. This should be called once per instance
    ///   of the service, and will be called during start-up. Calling it
    ///   multiple times will have no effect.
    /// </summary>
    /// <param name="observers">The playback observers.</param>
    void AddObserverParts(IEnumerable<IPlaybackObserver> observers);

    /// <summary>
    ///   List out all available transforms and their enabled/priority state.
    /// </summary>
    /// <param name="onlyEnabled">If true, only enabled transforms are returned.</param>
    IEnumerable<VideoStreamTransformInfo> GetAvailableTransforms(bool onlyEnabled = false);

    /// <summary>
    ///   List out all available transforms that also report support for the
    ///   given video via <see cref="IVideoStreamTransform.SupportsVideo"/>.
    /// </summary>
    /// <param name="video">The video to check applicability for.</param>
    /// <param name="context">The stream transform context.</param>
    /// <param name="onlyEnabled">If true, only enabled transforms are returned.</param>
    IEnumerable<VideoStreamTransformInfo> GetApplicableTransforms(IVideo video, VideoStreamTransformContext context, bool onlyEnabled = true);

    /// <summary>
    ///   Selects a transform to use for the given video. If
    ///   <paramref name="explicitTransformId"/> is provided, that transform is
    ///   used if it's enabled and applicable; otherwise the highest-priority
    ///   enabled and applicable transform is selected automatically.
    /// </summary>
    /// <param name="video">The video to select a transform for.</param>
    /// <param name="context">The stream transform context.</param>
    /// <param name="explicitTransformId">Optional. An explicit transform to use.</param>
    /// <returns>The selected transform info, or <c>null</c> if none is applicable.</returns>
    VideoStreamTransformInfo? SelectTransform(IVideo video, VideoStreamTransformContext context, Guid? explicitTransformId = null);

    /// <summary>
    ///   Gets the <see cref="VideoStreamTransformInfo"/> for the specified ID.
    /// </summary>
    VideoStreamTransformInfo? GetTransformInfo(Guid transformID);

    /// <summary>
    ///   Edit the settings for one or more transforms, such as whether it's
    ///   enabled, and its priority during automatic selection.
    /// </summary>
    void UpdateTransforms(params VideoStreamTransformInfo[] transforms);

    /// <summary>
    ///   List out all available playback observers and their enabled state.
    /// </summary>
    /// <param name="onlyEnabled">If true, only enabled observers are returned.</param>
    IEnumerable<PlaybackObserverInfo> GetAvailableObservers(bool onlyEnabled = false);

    /// <summary>
    ///   Gets the <see cref="PlaybackObserverInfo"/> for the specified ID.
    /// </summary>
    PlaybackObserverInfo? GetObserverInfo(Guid observerID);

    /// <summary>
    ///   Edit the enabled state for one or more playback observers.
    /// </summary>
    void UpdateObservers(params PlaybackObserverInfo[] observers);

    /// <summary>
    ///   Notifies all enabled playback observers of a unit of playback
    ///   progress. Each observer is dispatched independently; a failing
    ///   observer does not affect the others or the stream response.
    /// </summary>
    /// <param name="context">The playback progress context.</param>
    Task NotifyPlaybackProgress(PlaybackProgressContext context);
}
