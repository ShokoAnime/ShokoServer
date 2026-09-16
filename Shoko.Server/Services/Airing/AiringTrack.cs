using System;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Enums;

#nullable enable
namespace Shoko.Server.Services.Airing;

/// <summary>
/// One track of a schedule, as it is handed back to a caller: the kind and
/// codes the provider submitted, plus the <see cref="TitleLanguage"/> inferred
/// from them.
/// </summary>
/// <remarks>
/// Tracks belong to a schedule and are stored as JSON on it, so this is a view
/// over one entry of that list rather than a row of its own.
/// </remarks>
internal sealed class AiringTrack : IAiringTrack
{
    /// <inheritdoc/>
    public AiringKind Kind { get; }

    /// <inheritdoc/>
    public string LanguageCode { get; }

    /// <inheritdoc/>
    public string? CountryCode { get; }

    /// <inheritdoc/>
    public TitleLanguage Language { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AiringTrack"/> class from
    /// the data stored on the schedule.
    /// </summary>
    /// <param name="data">The stored track.</param>
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is <see langword="null"/>.</exception>
    public AiringTrack(AiringTrackData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        Kind = data.Kind;
        LanguageCode = string.IsNullOrWhiteSpace(data.LanguageCode) ? "unk" : data.LanguageCode.Trim().ToLowerInvariant();
        CountryCode = string.IsNullOrWhiteSpace(data.CountryCode) ? null : data.CountryCode.Trim().ToUpperInvariant();
        Language = ResolveLanguage(LanguageCode, CountryCode);
    }

    /// <summary>
    /// Resolve the language of a track, preferring the regional form when the
    /// enum knows it, so <c>pt</c> + <c>BR</c> is Brazilian Portuguese rather
    /// than plain Portuguese.
    /// </summary>
    /// <param name="languageCode">The track's language code.</param>
    /// <param name="countryCode">The track's country code, if it has one.</param>
    /// <returns>The resolved language, or <see cref="TitleLanguage.Unknown"/> when neither form is known.</returns>
    private static TitleLanguage ResolveLanguage(string languageCode, string? countryCode)
    {
        if (countryCode is not null && $"{languageCode}-{countryCode}".ToUpperInvariant().TryGetTitleLanguage(out var regional))
            return regional;

        return languageCode.TryGetTitleLanguage(out var language) ? language : TitleLanguage.Unknown;
    }
}
