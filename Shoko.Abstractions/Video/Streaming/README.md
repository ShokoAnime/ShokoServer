# Video Stream Pipeline

This folder defines the public API surface for pre-processing and observing
video stream requests (`/api/v3/File/{fileID}/Stream*`). By default, nothing in
this pipeline is active: streams are served as raw byte-range passthrough with
zero overhead, exactly as before this abstraction existed. Plugins opt in to
either or both extension points below.

There are two independent, unrelated extension points:

| Interface | Purpose | Selection | Example |
|---|---|---|---|
| `IVideoStreamTransform` | Pre-processes the video into an HLS rendition (transcode, filter, interpolate) | At most one active per session — highest priority applicable, or explicit choice | ffmpeg transcode, RIFE frame interpolation |
| `IPlaybackObserver` | Observes playback progress for side effects, doesn't touch bytes | Every enabled observer runs on every request | Scrobbling |

---

## `IVideoStreamTransform` — pre-processing

A transform produces an `IStreamRendition`, which the core turns into an HLS
VOD manifest (`#EXT-X-PLAYLIST-TYPE:VOD`) and streams to the client as
`init.mp4` + `segment-{index}.m4s` requests. The core computes segment count
from the video's known duration and the rendition's `SegmentDuration` — a
transform does not need to track or report total duration or segment count
itself.

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
Emby, Plex) — it isn't novel, and the core abstraction deliberately stays out
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
invocation, redirected stdout/stderr — see `AVDumpHelper` in Shoko.Server for
the established pattern for shelling out to an external tool) and watches the
cache directory for produced segments, implementing the seek-restart strategy
above for out-of-window `OpenSegmentAsync` calls.

### Example: RIFE frame interpolation

Real-time frame interpolation via [RIFE](https://github.com/hzwer/ECCV2022-RIFE)
has viable server-side implementations — `rife-ncnn-vulkan` (CLI, image-sequence
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

Note: SVP and madVR are **not** viable targets for this abstraction — both are
client-side DirectShow/renderer plugins with no server-invokable binary, so
there is nothing for a server-side transform to shell out to.

### Registering

```csharp
services.AddSingleton<IVideoStreamTransform, FfmpegTranscodeTransform>();
```

---

## `IPlaybackObserver` — observing playback

An observer is notified after each byte-range (progressive) or segment (HLS)
is served, via `OnPlaybackProgress`. All enabled observers run on every
request — there's no priority ordering, since running an additional passive
observer alongside another is harmless.

```csharp
public class ScrobbleObserver(IUserDataService userDataService) : IPlaybackObserver
{
    public string Name => "Scrobble";

    public async Task OnPlaybackProgress(PlaybackProgressContext context, CancellationToken cancellationToken)
    {
        if (context.User is null) return;
        if (!context.IsFinalUnit) return; // or apply your own watched-percentage threshold using context.Position/TotalDuration

        await userDataService.SetVideoWatchedStatus(context.Video, context.User);
    }
}
```

For HLS playback, `context.Position` is a precise `segmentIndex * SegmentDuration`
value. For progressive playback, position is inferred from the requested byte
range reaching the end of the file — a heuristic, not a guarantee of actual
bytes delivered to the player.

### Reading query parameters

Both `VideoStreamTransformContext` and `PlaybackProgressContext` expose the
raw `QueryParameters` (an `IQueryCollection`) from the originating HTTP
request. This lets a plugin read parameters the core doesn't know about — a
quality/profile hint for a transform, or a legacy flag an observer wants to
stay compatible with — without the stream endpoints needing a dedicated
parameter for every plugin.

The built-in `ScrobbleObserver` (`Shoko.Server.Streaming.ScrobbleObserver`,
disabled by default like any other observer) uses this to stay
backwards-compatible with the old `/Stream?streamPositionScrobbling=true`
per-request flag: for `PlaybackKind.Progressive` it only marks a video watched
if that query parameter is present and `true`, matching the exact behavior
the flag used to have on the now-removed `ScrobblingFileResult`. For
`PlaybackKind.Hls` there's no legacy flag to honor — requesting an HLS
manifest is itself an explicit opt-in — so it acts whenever the observer is
enabled.

### Registering

```csharp
services.AddSingleton<IPlaybackObserver, ScrobbleObserver>();
```
