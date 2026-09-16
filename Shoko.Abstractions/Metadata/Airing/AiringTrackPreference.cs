namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   One entry in an ordered track preference, used to pick the one airing a
///   caller wants.
/// </summary>
/// <param name="Kind">
///   The preferred kind.
/// </param>
/// <param name="LanguageCode">
///   Optional. The preferred language, as a language code. <c>null</c> matches
///   any language of the kind.
/// </param>
public sealed record AiringTrackPreference(AiringKind Kind, string? LanguageCode = null);
