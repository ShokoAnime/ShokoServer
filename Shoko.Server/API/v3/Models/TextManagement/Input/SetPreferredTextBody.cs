using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.API.v3.Models.TextManagement.Input;

/// <summary>
///   Body for picking the text an entry is called by or described by. Name
///   exactly one of a stored text, the entry's default, or a new value.
/// </summary>
public class SetPreferredTextBody
{
    /// <summary>
    ///   Whether a title or an overview is picked. Required: title and overview
    ///   IDs overlap, so the kind is never guessed.
    /// </summary>
    [Required]
    public TextKind? Kind { get; set; }

    /// <summary>
    ///   The ID of a stored text of the same kind, on this entry or any
    ///   other, such as a title of the AniDB anime behind a Shoko series.
    /// </summary>
    public int? TextID { get; set; }

    // DefaultValueHandling.Populate sets an omitted member to its [DefaultValue]
    // (or the type's default) over the initializer, so each default is also an attribute.

    /// <summary>
    ///   Pick the entry's default, such as the title kept on its own row.
    ///   Defaults to <c>false</c>.
    /// </summary>
    [DefaultValue(false)]
    public bool Default { get; set; }

    /// <summary>
    ///   A new value to pick, stored on the entry as a <c>user</c> text.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    ///   The language code of a new value. Defaults to <c>unk</c>.
    /// </summary>
    [DefaultValue("unk")]
    public string LanguageCode { get; set; } = "unk";

    /// <summary>
    ///   The country code of a new value, if any.
    /// </summary>
    public string? CountryCode { get; set; }

    /// <summary>
    ///   Prefer the text only once the language order reaches its language,
    ///   rather than over everything. Defaults to <c>false</c>.
    /// </summary>
    [DefaultValue(false)]
    public bool LanguageOnly { get; set; }
}
