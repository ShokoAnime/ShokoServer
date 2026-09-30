# Video Stream Pipeline

The API for pre-processing and observing video stream requests
(`/api/v3/File/{fileID}/Stream*`). No transform is active by default: streams
are served as raw byte-range passthrough. There are two independent extension
points:

| Interface | Purpose | Selection | Example |
|---|---|---|---|
| `IVideoStreamTransform` | Pre-processes the video into an HLS rendition (transcode, filter, interpolate) | At most one active per session: the highest-priority applicable one, or an explicit choice | ffmpeg transcode, RIFE frame interpolation |
| `IPlaybackObserver` | Observes playback progress for side effects, doesn't touch bytes | Every enabled observer runs on every request | Scrobbling |

Transforms and plugin observers start disabled. Observers from the core plugin
start enabled (see [the built-in
`LegacyScrobbleObserver`](#reading-query-parameters)).

---

## `IVideoStreamTransform`: pre-processing

A transform produces an `IStreamRendition` (see [Delivery shapes](#delivery-shapes)).
The core turns an `IHlsStreamRendition` into an HLS VOD manifest and serves it
as `init.mp4` + `segment-{index}.m4s` requests, computing the segment count from
the video's duration and the rendition's `SegmentDuration`.

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

A session idle for `SessionIdleTimeoutMinutes` is evicted (swept once a
minute) and its rendition disposed. A client asking for it again within
`EvictedSessionResumeHours` gets a new rendition from the same transform, video
and starting query string under the same session ID, so a rendition should
produce the same playlists and segments when rebuilt.

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

### Refusing a request

`OpenResourceAsync` and `IProgressiveStreamRendition.OpenAsync` can return
`null`, which the client sees as a bare `404`. To tell the client why, throw one
of these instead, and its message is sent back as the reason:

| Exception | Response | For example |
|---|---|---|
| `StreamResourceNotFoundException` | `404` | the file has no such track |
| `StreamResourceUnsupportedException` | `400` | the codec cannot be carried in the container |
| `StreamResourceNotReadyException` | `503`, with `Retry-After` when `RetryAfter` is set | the segment is still being produced |

Prefer refusing a resource that is not ready over waiting for it: players
retry a `503` with `Retry-After`, while a wait past `SegmentRequestTimeoutSeconds`
answers `504`, which a player may treat as fatal.

Every other exception is a server error, a bare `NotSupportedException`
included; wrap it in `StreamResourceUnsupportedException` where it really is
about the request.

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

The abstraction does not require this; it is what on-demand HLS transcoders
usually do.

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

`FfmpegRendition` launches ffmpeg once per session, watches the cache directory
for produced segments, and restarts on out-of-window `OpenSegmentAsync` calls as
above.

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

SVP and madVR are client-side renderer plugins with no server-side binary, so
they are not targets for a transform.

### Registering

A transform normally needs no DI registration. The core discovers the type,
builds it with constructor injection, and the pipeline keeps that instance for
the life of the process. Register the **concrete** type as a singleton only
when your own code resolves the transform, and never register it under
`IVideoStreamTransform`; see [Contracts the server discovers for
you](../../README.md#contracts-the-server-discovers-for-you).

A transform that needs user-editable settings implements
`IVideoStreamTransform<TConfiguration>` where
`TConfiguration : IVideoStreamTransformConfiguration`, as
`FfmpegTranscodeTransform` does above.

---

## `IPlaybackObserver`: observing playback

An observer is notified through `OnPlaybackProgress` for each byte-range
(progressive) or segment (HLS) request, before that response is sent. Enabled
observers run one after another, awaited with no cancellation token, so the
request waits on each in turn; a throwing observer is logged and skipped. Keep
it fast, and hand anything slow to a queue job.

```csharp
public class LegacyScrobbleObserver(IUserDataService userDataService) : IPlaybackObserver
{
    public string Name => "Legacy Scrobbler";

    public async Task OnPlaybackProgress(PlaybackProgressContext context, CancellationToken cancellationToken)
    {
        if (context.User is null || !context.IsFinalUnit)
            return;

        // Progressive streams only with the legacy opt-in flag, see below.
        if (context.Kind is PlaybackKind.Progressive && !IsLegacyFlagSet(context))
            return;

        await userDataService.SetVideoWatchedStatus(context.Video, context.User);
    }

    private static bool IsLegacyFlagSet(PlaybackProgressContext context)
        => context.QueryParameters.TryGetValue("streamPositionScrobbling", out var value) && bool.TryParse(value, out var on) && on;
}
```

**`Position` is where the player fetched, not where the viewer is.** A player
reads ahead, may fetch segments it never shows, and does not re-request what it
has cached, so what arrives is the sequence of cache misses, jumping forwards
and backwards. Treat it as an upper bound on progress. For HLS it is
`segmentIndex * SegmentDuration`, or the segment's `StreamResource.Position`
from an `IHlsPresentationRendition`; for progressive playback it is inferred
from the requested byte range. A reliable position has to come from the client,
which is what the dedicated `/Scrobble` endpoint is for.

### Reading query parameters

Both `VideoStreamTransformContext` and `PlaybackProgressContext` expose the
raw `QueryParameters` of the originating request, so a plugin can read
parameters the core doesn't know about, such as a quality hint for a transform.

The built-in `LegacyScrobbleObserver` uses this for the old
`/Stream?streamPositionScrobbling=true` flag: for `PlaybackKind.Progressive` it
only marks a video watched when that parameter is `true`. For `PlaybackKind.Hls`
it acts whenever the observer is enabled, since requesting a manifest is itself
an opt-in. It ships enabled because it stands in for behaviour the endpoints
used to hardcode.

### Registering

Like a transform, an observer usually needs no registration: it is discovered,
constructed with constructor injection, and the pipeline holds the single
instance every request goes through. Register the **concrete** type as a
singleton only if your own code talks to it, and never under
`IPlaybackObserver`; see [Contracts the server discovers for
you](../../README.md#contracts-the-server-discovers-for-you).
