using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Text;

namespace Shoko.Server.API.v3.Models.TextManagement.Input;

/// <summary>
///   Body for changing a stored text in part. Every member left out or
///   <c>null</c> keeps its current value.
/// </summary>
public class UpdateTextBody
{
    /// <summary>
    ///   Whether the text may be listed and chosen.
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    ///   How strongly the text is preferred for its entry. Setting one clears
    ///   the same preference on the entry's other texts of the kind, and for
    ///   <c>Language</c> only on those in the same language.
    /// </summary>
    public TextPreference? Preference { get; set; }

    /// <summary>
    ///   The new value. Only a text a user added, that picks no other text,
    ///   can be given one.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    ///   The new position among the texts of the same kind its source gave
    ///   the entry.
    /// </summary>
    public int? Ordering { get; set; }

    /// <summary>
    ///   The change to hand the text manager.
    /// </summary>
    /// <returns>The update data.</returns>
    public TextUpdateData ToTextUpdateData()
        => new()
        {
            IsEnabled = IsEnabled,
            Preference = Preference,
            Value = Value,
            Ordering = Ordering,
        };
}
