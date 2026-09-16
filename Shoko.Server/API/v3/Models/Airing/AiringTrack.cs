using System;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Enums;

#nullable enable
namespace Shoko.Server.API.v3.Models.Airing;

/// <summary>
/// What an airing schedule releases. Tracks belong to the schedule, and its
/// airings repeat them for convenience, so a client can label a row without
/// fetching the schedule.
/// </summary>
/// <param name="track">The track.</param>
/// <exception cref="ArgumentNullException"><paramref name="track"/> is <c>null</c>.</exception>
public class AiringTrack(IAiringTrack track)
{
    /// <summary>
    /// The kind of the track.
    /// </summary>
    [Required]
    public AiringKind Kind { get; init; } = track.Kind;

    /// <summary>
    /// The language of the track, as a language code. <c>unk</c> when unknown.
    /// </summary>
    [Required]
    public string LanguageCode { get; init; } = track.LanguageCode;

    /// <summary>
    /// The country the track is for, as an ISO 3166-1 alpha-2 code, or
    /// <c>null</c> when it is not region specific.
    /// </summary>
    public string? CountryCode { get; init; } = track.CountryCode;

    /// <summary>
    /// The language of the track, inferred from <see cref="LanguageCode"/> and
    /// <see cref="CountryCode"/>.
    /// </summary>
    [Required]
    public TitleLanguage Language { get; init; } = track.Language;
}
