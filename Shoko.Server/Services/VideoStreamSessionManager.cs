using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Streaming;
using Shoko.Server.Settings;

namespace Shoko.Server.Services;

/// <summary>
///   Tracks active HLS stream sessions (a video paired with a selected
///   <see cref="IVideoStreamTransform"/>'s <see cref="IStreamRendition"/>),
///   evicting and cleaning up idle sessions.
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

    public Guid CreateSession(IVideo video, IStreamRendition rendition)
    {
        var sessionId = Guid.NewGuid();
        var cacheDir = Path.Combine(GetCacheRoot(), sessionId.ToString("N"));
        Directory.CreateDirectory(cacheDir);
        _sessions[sessionId] = new StreamSession(video, rendition, cacheDir);
        return sessionId;
    }

    public StreamSession? TryGetSession(Guid sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return null;

        session.Touch();
        return session;
    }

    public string BuildManifest(IVideo video, IStreamRendition rendition, Guid sessionId)
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

    public DateTime LastAccessedAt { get; private set; } = DateTime.UtcNow;

    public void Touch() => LastAccessedAt = DateTime.UtcNow;
}
