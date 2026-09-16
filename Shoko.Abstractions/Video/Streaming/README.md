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
`ScrobbleObserver`](#reading-query-parameters)).

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

A transform normally needs no DI registration. `PluginManager` finds every type
in your plugin assembly that implements `IVideoStreamTransform`, builds it with
`ActivatorUtilities.GetServiceOrCreateInstance` so constructor injection works
as usual, and hands the instance to `VideoStreamPipelineService`, which keeps it
for the life of the process. A transform that warms a cache or runs its own
internal timer is covered by this too, since the held instance is long lived.

Register the **concrete type** as a singleton only when your own code resolves
the transform, such as a controller or a cleanup job that has to reach the same
session bookkeeping:

```csharp
services.AddSingleton<FfmpegTranscodeTransform>();
```

Singleton is the lifetime that matters here: it is what makes your code and the
core share one transform. Registered as transient, your job gets a transform of
its own and the two drift apart.

Never register a transform under the `IVideoStreamTransform` interface:

```csharp
services.AddSingleton<IVideoStreamTransform, FfmpegTranscodeTransform>(); // don't
```

- It pollutes the container for everyone. Resolving a single `T` when several
  registrations exist returns the *last* one registered, so whichever plugin
  loads last silently wins and `GetRequiredService<IVideoStreamTransform>()`
  hands the caller an arbitrary plugin's transform.
- The core never reads that registration, because `GetExports<T>` asks the
  container for the concrete type.
- So a second instance gets constructed. Process handles, cache directories and
  warn-once flags you expect to be singleton state then split across two
  objects, and the instance you resolve from DI is not the one serving the
  stream.

---

## `IPlaybackObserver`: observing playback

An observer is notified after each byte-range (progressive) or segment (HLS)
is served, via `OnPlaybackProgress`. All enabled observers run on every
request, with no priority ordering, since running an additional passive
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
value, or whatever an `IHlsPresentationRendition` reported on the segment's
`StreamResource.Position`. For progressive playback, position is inferred from
the requested byte range reaching the end of the file, a heuristic rather than a
guarantee of actual bytes delivered to the player.

### Reading query parameters

Both `VideoStreamTransformContext` and `PlaybackProgressContext` expose the
raw `QueryParameters` (an `IQueryCollection`) from the originating HTTP
request. This lets a plugin read parameters the core doesn't know about, such as
a quality/profile hint for a transform or a legacy flag an observer wants to
stay compatible with, without the stream endpoints needing a dedicated
parameter for every plugin.

The built-in `ScrobbleObserver` (`Shoko.Server.Streaming.ScrobbleObserver`) uses
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

Like a transform, an observer usually needs no registration. `PluginManager`
discovers the type and constructs it with constructor injection (that is how the
`ScrobbleObserver` above receives its `IUserDataService`), and
`VideoStreamPipelineService` holds that single instance for the life of the
process. Every request goes through the same object.

Register the **concrete type** as a singleton only if something else in your
plugin talks to the observer directly, say a controller reporting the counters
it has accumulated:

```csharp
services.AddSingleton<ScrobbleObserver>();
```

The singleton lifetime is what keeps that shared. Registered as transient, your
controller reads the counters of a second observer that never saw a request.

Never register an observer under the `IPlaybackObserver` interface:

```csharp
services.AddSingleton<IPlaybackObserver, ScrobbleObserver>(); // don't
```

- It pollutes the container for everyone. Resolving a single `T` when several
  registrations exist returns the *last* one registered, so whichever plugin
  loads last silently wins and `GetRequiredService<IPlaybackObserver>()` hands
  the caller an arbitrary plugin's observer.
- The core never reads that registration, because `GetExports<T>` asks the
  container for the concrete type.
- So a second instance gets constructed. Whatever the observer accumulates
  across requests lives on the instance the pipeline calls, not on the one you
  resolved.
