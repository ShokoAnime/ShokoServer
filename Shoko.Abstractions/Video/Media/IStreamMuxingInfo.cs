
namespace Shoko.Abstractions.Video.Media;

/// <summary>
/// Stream muxing information.
/// </summary>
public interface IStreamMuxingInfo
{
    /// <summary>
    /// Raw muxing mode value.
    /// </summary>
    public string? Raw { get; }
}
