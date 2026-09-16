using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   What an airing schedule releases. Tracks belong to the schedule, and its
///   airings expose them for convenience.
/// </summary>
public interface IAiringTrack
{
    /// <summary>
    ///   The kind of the track.
    /// </summary>
    AiringKind Kind { get; }

    /// <summary>
    ///   The language of the track, as a language code. <c>"unk"</c> when
    ///   unknown.
    /// </summary>
    string LanguageCode { get; }

    /// <summary>
    ///   The country the track is for, as an ISO 3166-1 alpha-2 code, or
    ///   <c>null</c> when it is not region specific.
    /// </summary>
    string? CountryCode { get; }

    /// <summary>
    ///   The language of the track, inferred from
    ///   <see cref="LanguageCode"/> and <see cref="CountryCode"/>.
    /// </summary>
    TitleLanguage Language { get; }
}
