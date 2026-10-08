using Newtonsoft.Json.Linq;

namespace Shoko.Abstractions.UI;

/// <summary>
/// One value the server offers for an element whose
/// <see cref="UiElement.OptionsRoute"/> is set.
/// </summary>
/// <remarks>
/// Options keep the order the provider listed them in, duplicates included,
/// with nulls left out.
/// </remarks>
public class UiOption
{
    /// <summary>
    /// The value, serialised the way the configuration or action itself
    /// serialises it.
    /// </summary>
    public JToken? Value { get; init; }

    /// <summary>
    /// The label to show for the value: the provider's own, or the value in
    /// text form when it gave none.
    /// </summary>
    public required string Label { get; init; }
}
