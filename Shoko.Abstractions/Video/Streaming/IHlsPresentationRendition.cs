namespace Shoko.Abstractions.Video.Streaming;

/// <summary>
///   An <see cref="IStreamRendition"/> delivered as HLS where the rendition,
///   not the core, owns the whole presentation: the master playlist, every
///   media playlist it names, and every init and media segment those name.
///   Use it instead of <see cref="IHlsStreamRendition"/> when the core's
///   built-in playlist cannot describe the output, e.g. alternate audio
///   renditions (<c>EXT-X-MEDIA</c>), a bitrate ladder of variants, or
///   segments cut at irregular keyframe positions.
/// </summary>
/// <remarks>
///   The core mints the session, then redirects the client to
///   <c>Stream/Hls/{sessionID}/master.m3u8</c>. Every request under that
///   session path, including the master playlist itself, is handed to
///   <see cref="IStreamRenditionResources.OpenResourceAsync"/>, so playlist
///   URIs should be relative to it.
/// </remarks>
public interface IHlsPresentationRendition : IStreamRendition, IStreamRenditionResources;
