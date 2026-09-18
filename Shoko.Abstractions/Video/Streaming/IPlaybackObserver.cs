using System.Threading;
using System.Threading.Tasks;

namespace Shoko.Abstractions.Video.Streaming;

/// <summary>
///   Base interface for all playback observers to implement. Observers are a
///   lightweight, side-effect-only extension point — unlike
///   <see cref="IVideoStreamTransform"/>, every enabled observer runs on every
///   stream request, since observing playback (e.g. for scrobbling) has no
///   meaningful notion of priority or exclusivity.
/// </summary>
public interface IPlaybackObserver
{
    /// <summary>
    ///   Friendly name of the observer.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///   Optional. Description of the observer.
    /// </summary>
    string? Description => null;

    /// <summary>
    ///   Called after a byte-range (progressive) or segment (HLS) has been
    ///   served to a client.
    /// </summary>
    /// <param name="context">The playback progress context.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task OnPlaybackProgress(PlaybackProgressContext context, CancellationToken cancellationToken);
}
