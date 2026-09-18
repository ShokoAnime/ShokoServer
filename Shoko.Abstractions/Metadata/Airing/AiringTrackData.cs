namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   A track a provider submits for an airing schedule. A language released at
///   another time is its own schedule, so the set is fixed for the run.
/// </summary>
/// <param name="Kind">
///   The kind of the track. It must be declared by the provider submitting it.
/// </param>
/// <param name="LanguageCode">
///   The language of the track, as a language code. Defaults to <c>"unk"</c>
///   when unknown.
/// </param>
/// <param name="CountryCode">
///   Optional. The country the track is for, as an ISO 3166-1 alpha-2 code,
///   used together with the language code to infer the
///   <see cref="IAiringTrack.Language"/>.
/// </param>
public sealed record AiringTrackData(AiringKind Kind, string LanguageCode = "unk", string? CountryCode = null);
