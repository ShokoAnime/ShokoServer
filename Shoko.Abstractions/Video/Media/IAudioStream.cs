
namespace Shoko.Abstractions.Video.Media;

/// <summary>
/// Audio stream metadata.
/// </summary>
public interface IAudioStream : IStream
{
    /// <summary>
    /// Number of total channels in the audio stream.
    /// </summary>
    int Channels { get; }

    /// <summary>
    /// A text representation of the layout of the channels available in the
    /// audio stream.
    /// </summary>
    string ChannelLayout { get; }

    /// <summary>
    /// Samples per frame.
    /// </summary>
    int SamplesPerFrame { get; }

    /// <summary>
    /// Sampling rate of the audio.
    /// </summary>
    int SamplingRate { get; }

    /// <summary>
    /// Compression mode used.
    /// </summary>
    string CompressionMode { get; }

    /// <summary>
    /// Dialog norm of the audio stream, if available.
    /// </summary>
    double? DialogNorm { get; }

    /// <summary>
    /// Bit-rate of the audio-stream.
    /// </summary>
    int BitRate { get; }

    /// <summary>
    /// Bit-rate mode of the audio stream.
    /// </summary>
    string BitRateMode { get; }

    /// <summary>
    /// Bit-depth of the audio stream.
    /// </summary>
    int BitDepth { get; }
}
