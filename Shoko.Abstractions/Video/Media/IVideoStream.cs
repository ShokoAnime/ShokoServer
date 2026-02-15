
namespace Shoko.Abstractions.Video.Media;

/// <summary>
/// Video stream information.
/// </summary>
public interface IVideoStream : IStream
{
    /// <summary>
    /// Width of the video stream.
    /// </summary>
    int Width { get; }

    /// <summary>
    /// Height of the video stream.
    /// </summary>
    int Height { get; }

    /// <summary>
    /// Standardized resolution.
    /// </summary>
    string Resolution { get; }

    /// <summary>
    /// Pixel aspect-ratio.
    /// </summary>
    decimal PixelAspectRatio { get; }

    /// <summary>
    /// Frame-rate.
    /// </summary>
    decimal FrameRate { get; }

    /// <summary>
    /// Frame-rate mode.
    /// </summary>
    string FrameRateMode { get; }

    /// <summary>
    /// Total number of frames in the video stream.
    /// </summary>
    int FrameCount { get; }

    /// <summary>
    /// Scan-type. Interlaced or progressive.
    /// </summary>
    string ScanType { get; }

    /// <summary>
    /// Color-space.
    /// </summary>
    string ColorSpace { get; }

    /// <summary>
    /// Chroma sub-sampling.
    /// </summary>
    string ChromaSubsampling { get; }

    /// <summary>
    /// Matrix co-efficiency.
    /// </summary>
    string? MatrixCoefficients { get; }

    /// <summary>
    /// Bit-rate of the video stream.
    /// </summary>
    int BitRate { get; }

    /// <summary>
    /// Bit-depth of the video stream.
    /// </summary>
    int BitDepth { get; }

    /// <summary>
    /// How the stream is muxed in the media container.
    /// </summary>
    IStreamMuxingInfo Muxing { get; }
}
