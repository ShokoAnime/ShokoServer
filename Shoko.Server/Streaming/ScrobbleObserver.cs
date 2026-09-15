using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.User.Services;
using Shoko.Abstractions.Video.Streaming;

namespace Shoko.Server.Streaming;

/// <summary>
///   Built-in playback observer that marks a video as watched once playback
///   reaches the end. This is a core plugin — disabled by default — and is
///   discovered/toggled the same way any other <see cref="IPlaybackObserver"/>
///   would be (see <see cref="Shoko.Abstractions.Video.Services.IVideoStreamPipelineService"/>).
/// </summary>
/// <remarks>
///   For <see cref="PlaybackKind.Progressive"/> streams this only acts when
///   the request carries the legacy <c>streamPositionScrobbling=true</c>
///   query parameter, preserving the exact backwards-compatible, per-request
///   opt-in behavior of the old <c>ScrobblingFileResult</c>/<c>/Stream</c>
///   flag for players that don't support the dedicated <c>/Scrobble</c>
///   endpoint. For <see cref="PlaybackKind.Hls"/> streams there is no legacy
///   flag to honor — a client using HLS has already opted in by requesting a
///   transform — so it acts whenever the observer is enabled.
/// </remarks>
public class ScrobbleObserver(IUserDataService userDataService) : IPlaybackObserver
{
    private const string LegacyQueryParameterName = "streamPositionScrobbling";

    public string Name => "Scrobble";

    public string? Description =>
        "Marks a video as watched when playback reaches the end. For progressive streams, this only applies " +
        $"when the request includes the legacy `{LegacyQueryParameterName}=true` query parameter, for backwards " +
        "compatibility with players that don't support the dedicated Scrobble endpoint.";

    public async Task OnPlaybackProgress(PlaybackProgressContext context, CancellationToken cancellationToken)
    {
        if (context.User is null || !context.IsFinalUnit)
            return;

        if (context.Kind is PlaybackKind.Progressive && !IsLegacyFlagSet(context))
            return;

        await userDataService.SetVideoWatchedStatus(context.Video, context.User);
    }

    private static bool IsLegacyFlagSet(PlaybackProgressContext context)
        => context.QueryParameters.TryGetValue(LegacyQueryParameterName, out var value) && bool.TryParse(value, out var parsed) && parsed;
}
