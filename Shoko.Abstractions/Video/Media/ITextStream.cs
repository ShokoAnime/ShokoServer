
namespace Shoko.Abstractions.Video.Media;

/// <summary>
/// Text stream information.
/// </summary>
public interface ITextStream : IStream
{
    /// <summary>
    /// Sub-title of the text stream.
    /// </summary>
    /// <value></value>
    string? SubTitle { get; }

    /// <summary>
    /// Not From MediaInfo. Is this an external sub file
    /// </summary>
    bool IsExternal { get; }

    /// <summary>
    /// The name of the external subtitle file if this is stream is from an
    /// external source. This field is only sent if <see cref="IsExternal"/>
    /// is set to <code>true</code>.
    /// </summary>
    string? ExternalFilename { get; }
}
