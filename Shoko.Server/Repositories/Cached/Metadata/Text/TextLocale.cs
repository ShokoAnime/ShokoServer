using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Repositories.Cached.Metadata.Text;

/// <summary>
///   A language and the codes a stored text gives it, kept once in the text
///   cache however many texts share it.
/// </summary>
/// <param name="Language">The language, as worked out from the codes.</param>
/// <param name="LanguageCode">The language code, or <c>unk</c>.</param>
/// <param name="CountryCode">The country code, when there is one.</param>
/// <param name="ScriptCode">The ISO 15924 script code, when there is one.</param>
internal sealed record TextLocale(TitleLanguage Language, string LanguageCode, string? CountryCode, string? ScriptCode);
