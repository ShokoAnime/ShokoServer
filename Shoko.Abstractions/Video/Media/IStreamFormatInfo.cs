
namespace Shoko.Abstractions.Video.Media;

/// <summary>
/// Stream format information.
/// </summary>
public interface IStreamFormatInfo
{
    /// <summary>
    /// Name of the format used.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Profile name of the format used, if available.
    /// </summary>
    string? Profile { get; }

    /// <summary>
    /// Compression level of the format used, if available.
    /// </summary>
    string? Level { get; }

    /// <summary>
    /// Format settings, if available.
    /// </summary>
    string? Settings { get; }

    /// <summary>
    /// Known additional features enabled for the format, if available.
    /// </summary>
    string? AdditionalFeatures { get; }

    /// <summary>
    /// Format endianness, if available.
    /// </summary>
    string? Endianness { get; }

    /// <summary>
    /// Format tier, if available.
    /// </summary>
    string? Tier { get; }

    /// <summary>
    /// Format commercial information, if available.
    /// </summary>
    string? Commercial { get; }

    /// <summary>
    /// HDR format information, if available.
    /// </summary>
    /// <remarks>
    /// Only available for <see cref="IVideoStream"/>.
    /// </remarks>
    string? HDR { get; }

    /// <summary>
    /// HDR format compatibility information, if available.
    /// </summary>
    /// <remarks>
    /// Only available for <see cref="IVideoStream"/>.
    /// </remarks>
    string? HDRCompatibility { get; }

    /// <summary>
    /// Context-adaptive binary arithmetic coding (CABAC).
    /// </summary>
    /// <remarks>
    /// Only available for <see cref="IVideoStream"/>.
    /// </remarks>
    bool CABAC { get; }

    /// <summary>
    /// Bi-directional video object planes (BVOP).
    /// </summary>
    /// <remarks>
    /// Only available for <see cref="IVideoStream"/>.
    /// </remarks>
    bool BVOP { get; }

    /// <summary>
    /// Quarter-pixel motion (Qpel).
    /// </summary>
    /// <remarks>
    /// Only available for <see cref="IVideoStream"/>.
    /// </remarks>
    bool QPel { get; }

    /// <summary>
    /// Global Motion Compensation (GMC) mode, if available.
    /// </summary>
    /// <remarks>
    /// Only available for <see cref="IVideoStream"/>.
    /// </remarks>
    string? GMC { get; }

    /// <summary>
    /// Reference frames count, if known.
    /// </summary>
    /// <remarks>
    /// Only available for <see cref="IVideoStream"/>.
    /// </remarks>
    int? ReferenceFrames { get; }
}
