using Shoko.Abstractions.Enums;

namespace Shoko.Abstractions.Video.Media;

/// <summary>
/// Stream metadata.
/// </summary>
public interface IStream
{
    /// <summary>
    /// Local id for the stream.
    /// </summary>
    int ID { get; }

    /// <summary>
    /// Unique id for the stream.
    /// </summary>
    string UID { get; }

    /// <summary>
    /// Stream title, if available.
    /// </summary>
    string? Title { get; }

    /// <summary>
    /// Stream order.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Indicates this is the default stream of the given type.
    /// </summary>
    bool IsDefault { get; }

    /// <summary>
    /// Indicates the stream is forced to be used.
    /// </summary>
    bool IsForced { get; }

    /// <summary>
    /// <see cref="TitleLanguage"/> name of the language of the stream.
    /// </summary>
    TitleLanguage Language { get; }

    /// <summary>
    /// 3 character language code of the language of the stream.
    /// </summary>
    string? LanguageCode { get; }

    /// <summary>
    /// Stream codec information.
    /// </summary>
    IStreamCodecInfo Codec { get; }

    /// <summary>
    /// Stream format information.
    /// </summary>
    IStreamFormatInfo Format { get; }
}
