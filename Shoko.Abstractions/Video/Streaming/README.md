# Video Stream Pipeline

This folder defines the public API surface for pre-processing and observing
video stream requests (`/api/v3/File/{fileID}/Stream*`). No transform is active
by default: streams are served as raw byte-range passthrough with zero overhead,
exactly as before this abstraction existed. Plugins opt in to either or both
extension points below.

There are two independent, unrelated extension points:

| Interface | Purpose | Selection | Example |
|---|---|---|---|
| `IVideoStreamTransform` | Pre-processes the video into an HLS rendition (transcode, filter, interpolate) | At most one active per session: the highest-priority applicable one, or an explicit choice | ffmpeg transcode, RIFE frame interpolation |
| `IPlaybackObserver` | Observes playback progress for side effects, doesn't touch bytes | Every enabled observer runs on every request | Scrobbling |

Transforms are opt-in on both sides, and so are observers that come from a
plugin. The one exception is an observer shipped by the core plugin, which
defaults to enabled so that an install keeps behaving the way it did before the
behaviour moved out of the endpoint (see [the built-in
`LegacyScrobbleObserver`](#reading-query-parameters)).

---

## `IVideoStreamTransform`: pre-processing

A transform produces an `IStreamRendition` (see [Delivery shapes](#delivery-shapes)).
The core turns an `IHlsStreamRendition` into an HLS VOD manifest (`#EXT-X-PLAYLIST-TYPE:VOD`) and streams to the client as
`init.mp4` + `segment-{index}.m4s` requests. The core computes segment count
from the video's known duration and the rendition's `SegmentDuration`, so a
transform does not need to track or report total duration or segment count
itself.

### Delivery shapes

A transform's `DeliveryMode` decides which rendition interface it returns and
which endpoints serve it:

| Rendition | `DeliveryMode` | Entry point | Who writes the playlist |
|---|---|---|---|
| `IHlsStreamRendition` | `Hls` | `Stream/Hls/master.m3u8` | the core: one variant, fixed-duration segments |
| `IHlsPresentationRendition` | `Hls` | `Stream/Hls/master.m3u8` | the rendition: every request under `Stream/Hls/{sessionID}/` is passed to `OpenResourceAsync` |
| `IProgressiveStreamRendition` | `Progressive` | `Stream/Direct` | none: byte ranges of one file |

Both HLS shapes mint a session and redirect to
`Stream/Hls/{sessionID}/master.m3u8`, carrying the query string. Reach for
`IHlsPresentationRendition` when the core's manifest cannot describe the
output: alternate audio renditions, a bitrate ladder, or segments cut at
source keyframes rather than at a fixed duration. Playlist URIs it writes
should be relative, and should repeat any query parameters (such as `apikey`)
the next request needs.

A session that sits idle for `SessionIdleTimeoutMinutes` is evicted and its
rendition disposed. If a client asks for it again within
`EvictedSessionResumeHours`, the core builds a new rendition from the same
transform, video and starting query string under the same session ID, so a
long pause resumes rather than failing. A rendition should therefore produce
the same playlists and segments when rebuilt.

### Resources beside the stream

Any rendition can also implement `IStreamRenditionResources` to serve files
beside its stream, such as subtitle tracks and fonts. They are served under the
session, at `Stream/Hls/{sessionID}/{path}` or `Stream/Direct/{sessionID}/{path}`
depending on the delivery mode, and `OpenResourceAsync` receives the path and
the query string. `IHlsPresentationRendition` is this interface with
`master.m3u8` as its entry point.

`DescribeAsync` tells clients what is there: the video and audio tracks with
their ordinals, codecs, languages and per-user ranking, and the subtitles,
attachments (fonts), chapters and extras with their resource paths. The core
serves it as JSON at `Stream/Sessions/{sessionID}`, with every path resolved to
a URL carrying the request's query string, and points to it from the stream
responses with a `Link: <...>; rel="describedby"` header. Anything
rendition-specific goes in `StreamDescription.Metadata`.

### Segment production strategy

Implementations should run one long-lived background process per active
viewing window rather than spinning up a fresh process per segment or running
the whole file up front:

- Let the underlying tool own HLS segmenting (e.g. ffmpeg's own muxer:
  `-f hls -hls_segment_type fmp4 -hls_time N -hls_flags independent_segments -hls_list_size 0`)
  writing into a session-scoped cache directory, and watch that directory for
  newly produced segments to resolve `OpenSegmentAsync`.
- If a requested segment index is far outside the currently produced window
  (a seek), tear down the running process and restart it seeked to that
  timestamp, with a small backward overlap (~1-2s) so decoder/filter state can
  warm up before the requested segment.

This is the same approach used by other on-demand HLS transcoders (Jellyfin,
Emby, Plex). It isn't novel, and the core abstraction deliberately stays out
of the way of however a transform wants to do it.

### Example: ffmpeg transcode

```csharp
public class FfmpegTranscodeTransform : IVideoStreamTransform<FfmpegTranscodeConfiguration>
{
    public string Name => "ffmpeg Transcode";

    public bool SupportsVideo(IVideo video, VideoStreamTransformContext context)
        => video.MediaInfo is not null; // plus your own codec/container checks

    public async Task<IStreamRendition> GetRenditionAsync(
        IVideo video, VideoStreamTransformContext context, CancellationToken cancellationToken)
        => new FfmpegRendition(video, /* ffmpeg args, cache dir, ... */);
}
```

`FfmpegRendition` launches ffmpeg once per session (cross-platform process
invocation with redirected stdout/stderr; see `AVDumpHelper` in Shoko.Server for
the established pattern for shelling out to an external tool) and watches the
cache directory for produced segments, implementing the seek-restart strategy
above for out-of-window `OpenSegmentAsync` calls.

### Example: RIFE frame interpolation

Real-time frame interpolation via [RIFE](https://github.com/hzwer/ECCV2022-RIFE)
has viable server-side implementations: `rife-ncnn-vulkan` (CLI, image-sequence
based) or VapourSynth's `vs-rife` plugin. The recommended strategy prepends a
VapourSynth stage feeding ffmpeg's HLS muxer via a pipe, rather than an
image-sequence round trip through disk:

```
vspipe script.vpy - | ffmpeg -i pipe: -i <source> -map 0:v -map 1:a -f hls ...
```

`script.vpy` sources the segment's frame range (e.g. via `lsmash`/`ffms2`) and
applies `vs-rife` to interpolate additional frames (e.g. 24fps → 48/60fps)
before piping the result to ffmpeg for encoding + segmenting. The same
seek-restart strategy applies. `SupportsVideo` should gate on a cheap
GPU/Vulkan availability probe so a transform that will fail at runtime isn't
offered to clients or auto-selected.

Note: SVP and madVR are **not** viable targets for this abstraction. Both are
client-side DirectShow/renderer plugins with no server-invokable binary, so
there is nothing for a server-side transform to shell out to.

### Registering

A transform normally needs no DI registration. The core discovers the type,
builds it with constructor injection, and `VideoStreamPipelineService` keeps
that instance for the life of the process, which covers a transform that warms
a cache or runs its own internal timer. Register the **concrete** type as a
singleton only when your own code resolves the transform, such as a controller
or a cleanup job that has to reach the same session bookkeeping, and never
register it under `IVideoStreamTransform`, which would leave the pipeline
serving streams from a different instance than the one you resolved. The
reasons behind each branch are in [Contracts the server discovers for
you](../../README.md#contracts-the-server-discovers-for-you), in the plugin
overview.

A transform that needs user-editable settings implements
`IVideoStreamTransform<TConfiguration>` where
`TConfiguration : IVideoStreamTransformConfiguration`, as
`FfmpegTranscodeTransform` does above.

---

## `IPlaybackObserver`: observing playback

An observer is notified after each byte-range (progressive) or segment (HLS)
is served, via `OnPlaybackProgress`. All enabled observers run on every
request, with no priority ordering, since running an additional passive
observer alongside another is harmless.

```csharp
public class LegacyScrobbleObserver(IUserDataService userDataService) : IPlaybackObserver
{
    public string Name => "Legacy Scrobbler";

    public async Task OnPlaybackProgress(PlaybackProgressContext context, CancellationToken cancellationToken)
    {
        if (context.User is null) return;
        if (!context.IsFinalUnit) return; // Position/TotalDuration is fetch progress, not watch progress

        await userDataService.SetVideoWatchedStatus(context.Video, context.User);
    }
}
```

**`Position` is where the player fetched, not where the viewer is.** An
observer is called when bytes are served, and a player reads ahead of playback,
so a buffered segment, a seek, or a pre-fetch all look the same as viewing.
Treat it as an upper bound on progress rather than a measurement of it, and be
aware that a player can fetch the end of a file it never plays.

For HLS playback, `context.Position` is `segmentIndex * SegmentDuration`, or
whatever an `IHlsPresentationRendition` reported on the segment's
`StreamResource.Position`. The arithmetic is exact, but the value it describes
is a fetch, so it is not a precise playback position:

- A player requests segments ahead of what it is showing, and may request
  segments it never shows at all.
- A segment the player already holds is not requested again, so a seek into
  cached content produces no request and the server never sees it. A seek to a
  part of the stream that is not cached does produce one, and that request can
  be for an earlier point than the last, so the reported position jumps both
  forwards and backwards. What arrives is the sequence of cache misses, in
  whatever order the player happened to need them.
- A player jumping around a stream it has largely cached can therefore be
  almost invisible here, while a player that buffers aggressively looks further
  along than it is.

Progressive playback is weaker again: the position is inferred from the
requested byte range reaching the end of the file, which does not confirm the
bytes reached the player.

A reliable position has to come from the client saying where it is, which is
what the dedicated `/Scrobble` endpoint is for.

### Reading query parameters

Both `VideoStreamTransformContext` and `PlaybackProgressContext` expose the
raw `QueryParameters` (an `IQueryCollection`) from the originating HTTP
request. This lets a plugin read parameters the core doesn't know about, such as
a quality/profile hint for a transform or a legacy flag an observer wants to
stay compatible with, without the stream endpoints needing a dedicated
parameter for every plugin.

The built-in `LegacyScrobbleObserver` (`Shoko.Server.Streaming.LegacyScrobbleObserver`) uses
this to stay backwards-compatible with the old
`/Stream?streamPositionScrobbling=true` per-request flag: for
`PlaybackKind.Progressive` it only marks a video watched if that query parameter
is present and `true`, matching the exact behavior the flag used to have on the
now-removed `ScrobblingFileResult`. For `PlaybackKind.Hls` there's no legacy
flag to honor, since requesting an HLS manifest is itself an explicit opt-in, so
it acts whenever the observer is enabled.

It ships enabled, unlike a plugin-supplied observer, precisely because it stands
in for behaviour that used to be hardcoded into the endpoints it watches. That
default applies to any observer from the core plugin; observers from other
plugins stay opt-in.

### Registering

Like a transform, an observer usually needs no registration. It is discovered
and constructed with constructor injection (that is how the `LegacyScrobbleObserver`
above receives its `IUserDataService`), and `VideoStreamPipelineService` holds
the single instance every request goes through.

Register the **concrete** type as a singleton only if something else in your
plugin talks to the observer directly, say a controller reporting the counters
it has accumulated, and never register it under `IPlaybackObserver`: whatever
the observer accumulates across requests would then live on the instance the
pipeline calls rather than on the one you resolved. See [Contracts the server
discovers for you](../../README.md#contracts-the-server-discovers-for-you), in
the plugin overview.
