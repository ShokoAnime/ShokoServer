using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Streaming;
using Shoko.Server.Settings;

namespace Shoko.Server.Services;

/// <summary>
///   Tracks active stream sessions (a video paired with a selected
///   <see cref="IVideoStreamTransform"/>'s <see cref="IStreamRendition"/> --
///   HLS or progressive, per the transform's <c>DeliveryMode</c>), evicting
///   and cleaning up idle sessions.
/// </summary>
public class VideoStreamSessionManager(
    ILogger<VideoStreamSessionManager> logger,
    IApplicationPaths applicationPaths,
    ConfigurationProvider<VideoStreamPipelineSettings> configurationProvider
)
{
    private readonly ConcurrentDictionary<Guid, StreamSession> _sessions = new();

    /// <summary>
    ///   How long to wait for a requested segment to become available before
    ///   giving up, per <see cref="VideoStreamPipelineSettings.SegmentRequestTimeoutSeconds"/>.
    /// </summary>
    public int SegmentRequestTimeoutSeconds
        => configurationProvider.Load().SegmentRequestTimeoutSeconds;

    /// <summary>
    ///   Sessions that can be found again without the caller holding an id, looked up by an
    ///   opaque key the caller composes. See <see cref="TryGetSessionByKey"/>.
    /// </summary>
    private readonly ConcurrentDictionary<string, Guid> _keyedSessions = new();

    public Guid CreateSession(IVideo video, IStreamRendition rendition, string? key = null)
    {
        var sessionId = Guid.NewGuid();
        var cacheDir = Path.Combine(GetCacheRoot(), sessionId.ToString("N"));
        Directory.CreateDirectory(cacheDir);
        _sessions[sessionId] = new StreamSession(video, rendition, cacheDir) { Key = key };
        if (key is not null)
            _keyedSessions[key] = sessionId;
        return sessionId;
    }

    public StreamSession? TryGetSession(Guid sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return null;

        session.Touch();
        return session;
    }

    /// <summary>
    ///   Finds a session by the key it was created with, or <c>null</c> if there is none.
    /// </summary>
    /// <remarks>
    ///   Exists for the deprecated APIv1 <c>/Stream</c> routes. Every other entry point mints
    ///   a session, hands its id to the client, and gets it back on each subsequent request --
    ///   which is what keeps two viewers of one video on two independent renditions. A v1 URL
    ///   has nowhere to put an id and cannot redirect to one, because it is unauthenticated
    ///   while the v3 session route is not (unless
    ///   <c>Web.AllowAnonymousFileStreamingInAPIv3</c> says otherwise). Every byte-range
    ///   request on that route therefore arrives looking exactly like the last, and without a
    ///   key each one would start a fresh transcode.
    ///
    ///   The trade-off is inherent to that route rather than an oversight: callers sharing a
    ///   key share one rendition, so two clients playing the same video through v1 at
    ///   different positions will fight over its seek position. Compose the key to make that
    ///   as unlikely as the route allows, and prefer a real session id anywhere one can be
    ///   carried.
    /// </remarks>
    public StreamSession? TryGetSessionByKey(string key, out Guid sessionId)
    {
        sessionId = Guid.Empty;
        if (!_keyedSessions.TryGetValue(key, out var id))
            return null;

        // A key outliving its session is normal -- eviction happens by id, on idle. Drop the
        // stale mapping rather than resolving it to nothing forever.
        if (TryGetSession(id) is not { } session)
        {
            _keyedSessions.TryRemove(key, out _);
            return null;
        }

        sessionId = id;
        return session;
    }

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyGates = new();

    /// <summary>
    ///   Returns the session for <paramref name="key"/>, building one with
    ///   <paramref name="renditionFactory"/> if there is none. Returns <c>null</c> only if the
    ///   factory does.
    /// </summary>
    /// <remarks>
    ///   Single-flight per key, which is the point of it. A player opens several connections
    ///   at once and a keyed route cannot tell them apart, so an unguarded check-then-create
    ///   would start a transcode per connection and leak every one but the last.
    /// </remarks>
    public async Task<StreamSession?> GetOrCreateSessionAsync(
        string key,
        IVideo video,
        Func<CancellationToken, Task<IStreamRendition?>> renditionFactory,
        CancellationToken cancellationToken
    )
    {
        if (TryGetSessionByKey(key, out _) is { } existing)
            return existing;

        var gate = _keyGates.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            // Re-check under the gate: whoever we queued behind has probably just built it,
            // and building a second would orphan a running transcode nothing will ever read.
            if (TryGetSessionByKey(key, out _) is { } created)
                return created;

            if (await renditionFactory(cancellationToken) is not { } rendition)
                return null;

            return TryGetSession(CreateSession(video, rendition, key));
        }
        finally
        {
            gate.Release();
        }
    }

    public string BuildManifest(IVideo video, IHlsStreamRendition rendition, Guid sessionId)
    {
        var segmentSeconds = rendition.SegmentDuration.TotalSeconds;
        if (segmentSeconds <= 0)
            segmentSeconds = configurationProvider.Load().DefaultSegmentDurationSeconds;

        var totalSeconds = video.MediaInfo?.Duration.TotalSeconds ?? 0;
        var segmentCount = totalSeconds > 0 ? (int)Math.Ceiling(totalSeconds / segmentSeconds) : 0;

        var sb = new StringBuilder();
        sb.AppendLine("#EXTM3U");
        sb.AppendLine("#EXT-X-VERSION:7");
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"#EXT-X-TARGETDURATION:{(int)Math.Ceiling(segmentSeconds)}"));
        sb.AppendLine("#EXT-X-PLAYLIST-TYPE:VOD");
        sb.AppendLine("#EXT-X-MAP:URI=\"init.mp4\"");

        var remaining = totalSeconds;
        for (var index = 0; index < segmentCount; index++)
        {
            var duration = Math.Min(segmentSeconds, remaining);
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"#EXTINF:{duration:F3},"));
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"segment-{index}.m4s"));
            remaining -= duration;
        }

        sb.AppendLine("#EXT-X-ENDLIST");
        return sb.ToString();
    }

    public void EvictExpiredSessions(TimeSpan idleTimeout)
    {
        var cutoff = DateTime.UtcNow - idleTimeout;
        foreach (var (sessionId, session) in _sessions)
        {
            if (session.LastAccessedAt > cutoff)
                continue;

            if (!_sessions.TryRemove(sessionId, out _))
                continue;

            // Only if it still points here: a newer session may already have claimed the key.
            if (session.Key is { } key)
            {
                ((ICollection<KeyValuePair<string, Guid>>)_keyedSessions)
                    .Remove(new KeyValuePair<string, Guid>(key, sessionId));
                if (!_keyedSessions.ContainsKey(key) && _keyGates.TryRemove(key, out var gate))
                    gate.Dispose();
            }

            EvictSession(sessionId, session);
        }
    }

    private void EvictSession(Guid sessionId, StreamSession session)
        => Task.Run(async () =>
        {
            try
            {
                await session.Rendition.DisposeAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to dispose rendition for stream session {SessionID}.", sessionId);
            }

            try
            {
                if (Directory.Exists(session.CacheDir))
                    Directory.Delete(session.CacheDir, recursive: true);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete cache directory for stream session {SessionID}.", sessionId);
            }
        });

    private string GetCacheRoot()
    {
        var config = configurationProvider.Load();
        return string.IsNullOrEmpty(config.CacheDirectoryOverride) ? applicationPaths.StreamCachePath : config.CacheDirectoryOverride;
    }
}

/// <summary>
///   An active HLS stream session.
/// </summary>
public class StreamSession(IVideo video, IStreamRendition rendition, string cacheDir)
{
    public IVideo Video { get; } = video;

    public IStreamRendition Rendition { get; } = rendition;

    public string CacheDir { get; } = cacheDir;

    /// <summary>
    ///   The lookup key this session was created under, if any, so eviction can clean up the
    ///   mapping. Only the APIv1 stream routes use one -- see
    ///   <see cref="VideoStreamSessionManager.TryGetSessionByKey"/>.
    /// </summary>
    public string? Key { get; init; }

    public DateTime LastAccessedAt { get; private set; } = DateTime.UtcNow;

    public void Touch() => LastAccessedAt = DateTime.UtcNow;
}
