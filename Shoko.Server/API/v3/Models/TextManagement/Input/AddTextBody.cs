using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Text;

namespace Shoko.Server.API.v3.Models.TextManagement.Input;

/// <summary>
///   Body for adding a user's text to an entry.
/// </summary>
public class AddTextBody
{
    /// <summary>
    ///   Whether the text is a title or an overview. Required: title and overview
    ///   IDs overlap, so the kind is never guessed.
    /// </summary>
    [Required]
    public TextKind? Kind { get; set; }

    /// <summary>
    ///   The text itself. Must not be blank.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string Value { get; set; } = string.Empty;

    // DefaultValueHandling.Populate sets an omitted member to its [DefaultValue]
    // (or the type's default) over the initializer, so each default is also an attribute.

    /// <summary>
    ///   The language code: alpha 2, alpha 3 or an <c>x-</c> prefixed custom
    ///   code. Defaults to <c>unk</c>.
    /// </summary>
    [DefaultValue("unk")]
    public string LanguageCode { get; set; } = "unk";

    /// <summary>
    ///   The alpha 2 or alpha 3 country code, if any.
    /// </summary>
    public string? CountryCode { get; set; }

    /// <summary>
    ///   The ISO 15924 script code, such as <c>Latn</c>, if known.
    /// </summary>
    public string? ScriptCode { get; set; }

    /// <summary>
    ///   The kind of title. Ignored for an overview. Defaults to <c>None</c>.
    /// </summary>
    [DefaultValue(TitleType.None)]
    public TitleType TitleType { get; set; } = TitleType.None;

    /// <summary>
    ///   How strongly the text is preferred for its entry. Setting one clears
    ///   the same preference on the entry's other texts. Defaults to
    ///   <c>None</c>.
    /// </summary>
    [DefaultValue(TextPreference.None)]
    public TextPreference Preference { get; set; } = TextPreference.None;

    /// <summary>
    ///   Whether the text may be listed and chosen. Defaults to <c>true</c>.
    /// </summary>
    [DefaultValue(true)]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    ///   The text to hand the text manager, from the <c>user</c> source.
    /// </summary>
    /// <returns>The text data.</returns>
    /// <exception cref="InvalidOperationException">The body names no kind.</exception>
    public TextData ToTextData()
        => new()
        {
            Kind = Kind ?? throw new InvalidOperationException("The body names no kind."),
            Value = Value,
            LanguageCode = LanguageCode,
            CountryCode = CountryCode,
            ScriptCode = ScriptCode,
            TitleType = TitleType,
            Source = MetadataSource.User,
            Preference = Preference,
            IsEnabled = IsEnabled,
        };
}
