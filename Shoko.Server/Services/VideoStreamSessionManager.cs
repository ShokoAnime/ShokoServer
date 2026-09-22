using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
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

    /// <summary>
    ///   Evicted sessions that can still be rebuilt under their id, and when they were evicted.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, (StreamSessionSource Source, DateTime EvictedAt)> _evictedSessions = new();

    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _restoreGates = new();

    public Guid CreateSession(IVideo video, IStreamRendition rendition, string? key = null, StreamSessionSource? source = null)
    {
        var sessionId = Guid.NewGuid();
        AddSession(sessionId, video, rendition, key, source);
        return sessionId;
    }

    private StreamSession AddSession(Guid sessionId, IVideo video, IStreamRendition rendition, string? key, StreamSessionSource? source)
    {
        // Not named after the session: an evicted session's directory is deleted in the background, and may still be when it is
        // rebuilt under the same id.
        var cacheDir = Path.Combine(GetCacheRoot(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cacheDir);
        var session = new StreamSession(video, rendition, cacheDir) { Key = key, Source = source };
        _sessions[sessionId] = session;
        if (key is not null)
            _keyedSessions[key] = sessionId;
        return session;
    }

    /// <summary>
    ///   Returns the session for <paramref name="sessionId"/>, rebuilding it with <paramref name="renditionFactory"/> if it was evicted
    ///   within <see cref="VideoStreamPipelineSettings.EvictedSessionResumeHours"/>. Returns <c>null</c> if there is no such session, or
    ///   the factory returns <c>null</c>.
    /// </summary>
    /// <remarks>
    ///   Single-flight per session, since a player resuming after a long pause sends several requests at once.
    /// </remarks>
    public async Task<StreamSession?> GetOrRestoreSessionAsync(
        Guid sessionId,
        Func<StreamSessionSource, CancellationToken, Task<(IVideo Video, IStreamRendition Rendition)?>> renditionFactory,
        CancellationToken cancellationToken
    )
    {
        if (TryGetSession(sessionId) is { } existing)
            return existing;

        if (!_evictedSessions.ContainsKey(sessionId))
            return null;

        var gate = _restoreGates.GetOrAdd(sessionId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (TryGetSession(sessionId) is { } restored)
                return restored;

            if (!_evictedSessions.TryGetValue(sessionId, out var evicted))
                return null;

            if (await renditionFactory(evicted.Source, cancellationToken) is not { } result)
                return null;

            _evictedSessions.TryRemove(sessionId, out _);
            return AddSession(sessionId, result.Video, result.Rendition, null, evicted.Source);
        }
        finally
        {
            gate.Release();
            _restoreGates.TryRemove(new KeyValuePair<Guid, SemaphoreSlim>(sessionId, gate));
        }
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
    ///   Exists for the legacy APIv2 <c>/Stream</c> routes. Every other entry point mints
    ///   a session, hands its id to the client, and gets it back on each subsequent request --
    ///   which is what keeps two viewers of one video on two independent renditions. A legacy URL
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

    public string BuildManifest(IVideo video, IHlsStreamRendition rendition, string queryString)
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
        sb.AppendLine($"#EXT-X-MAP:URI=\"init.mp4{queryString}\"");

        var remaining = totalSeconds;
        for (var index = 0; index < segmentCount; index++)
        {
            var duration = Math.Min(segmentSeconds, remaining);
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"#EXTINF:{duration:F3},"));
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"segment-{index}.m4s{queryString}"));
            remaining -= duration;
        }

        sb.AppendLine("#EXT-X-ENDLIST");
        return sb.ToString();
    }

    public void EvictExpiredSessions(TimeSpan idleTimeout, TimeSpan? resumeWindow = null)
    {
        var now = DateTime.UtcNow;
        var resumeCutoff = now - (resumeWindow ?? TimeSpan.FromHours(configurationProvider.Load().EvictedSessionResumeHours));
        foreach (var (sessionId, evicted) in _evictedSessions)
        {
            if (evicted.EvictedAt <= resumeCutoff)
                ((ICollection<KeyValuePair<Guid, (StreamSessionSource, DateTime)>>)_evictedSessions)
                    .Remove(new KeyValuePair<Guid, (StreamSessionSource, DateTime)>(sessionId, evicted));
        }

        var cutoff = now - idleTimeout;
        foreach (var (sessionId, session) in _sessions)
        {
            if (session.LastAccessedAt > cutoff || session.IsInUse)
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

            if (session.Source is { } source)
                _evictedSessions[sessionId] = (source, now);

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
    ///   mapping. Only the legacy APIv2 stream routes use one -- see
    ///   <see cref="VideoStreamSessionManager.TryGetSessionByKey"/>.
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    ///   What the session was built from, if it can be rebuilt after eviction.
    /// </summary>
    public StreamSessionSource? Source { get; init; }

    /// <summary>
    ///   The ID of the transform that produced <see cref="Rendition"/>, if known.
    /// </summary>
    public string? TransformID => Source?.TransformID;

    public DateTime LastAccessedAt { get; private set; } = DateTime.UtcNow;

    private int _activeResponses;

    /// <summary>
    ///   Whether a response is still streaming from this session. A session in use is never evicted, however long
    ///   ago its last request arrived.
    /// </summary>
    public bool IsInUse => Volatile.Read(ref _activeResponses) > 0;

    public void Touch() => LastAccessedAt = DateTime.UtcNow;

    /// <summary>
    ///   Wraps a stream served from this session so the session stays in use until the response is done with it.
    /// </summary>
    public Stream Track(Stream stream, HttpResponse response)
    {
        Interlocked.Increment(ref _activeResponses);
        var tracked = new SessionStream(stream, this);
        response.RegisterForDisposeAsync(tracked);
        return tracked;
    }

    private void Release()
    {
        Interlocked.Decrement(ref _activeResponses);
        Touch();
    }

    private sealed class SessionStream(Stream inner, StreamSession session) : Stream
    {
        private int _disposed;

        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => inner.CanSeek;

        public override bool CanWrite => false;

        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override int Read(Span<byte> buffer) => inner.Read(buffer);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => inner.ReadAsync(buffer, offset, count, cancellationToken);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => inner.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

        public override void Flush() { }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref _disposed, 1) is 0)
            {
                inner.Dispose();
                session.Release();
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) is 0)
            {
                await inner.DisposeAsync();
                session.Release();
            }

            await base.DisposeAsync();
        }
    }
}
