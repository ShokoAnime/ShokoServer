using System;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Metadata.Airing;

#nullable enable
namespace Shoko.Server.API.v3.Models.Airing;

/// <summary>
/// One entry in the server's ordered track preference, used to pick the one
/// airing a caller wants when several cover the same episode.
/// </summary>
public class TrackPreference
{
    /// <summary>
    /// The preferred kind.
    /// </summary>
    [Required]
    public AiringKind Kind { get; set; }

    /// <summary>
    /// Optional. The preferred language, as a language code. <c>null</c>
    /// matches any language of the kind.
    /// </summary>
    public string? LanguageCode { get; set; }

    /// <summary>
    /// Initializes a new empty instance of the <see cref="TrackPreference"/>
    /// class, for deserialization.
    /// </summary>
    public TrackPreference() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="TrackPreference"/> class
    /// from a stored preference.
    /// </summary>
    /// <param name="preference">The stored preference.</param>
    /// <exception cref="ArgumentNullException"><paramref name="preference"/> is <c>null</c>.</exception>
    public TrackPreference(AiringTrackPreference preference)
    {
        ArgumentNullException.ThrowIfNull(preference);

        Kind = preference.Kind;
        LanguageCode = preference.LanguageCode;
    }

    /// <summary>
    /// Converts the preference to the shape the airing schedule service reads.
    /// </summary>
    /// <returns>The stored preference.</returns>
    public AiringTrackPreference ToPreference()
        => new(Kind, string.IsNullOrWhiteSpace(LanguageCode) ? null : LanguageCode.Trim().ToLowerInvariant());
}
