
namespace Shoko.Abstractions.Video.Media;

/// <summary>
/// Stream codec information.
/// </summary>
public interface IStreamCodecInfo
{
    /// <summary>
    /// Codec name, if available.
    /// </summary>
    string? Name { get; }

    /// <summary>
    /// Simplified codec id.
    /// </summary>
    string Simplified { get; }

    /// <summary>
    /// Raw codec id.
    /// </summary>
    string? Raw { get; }
}
