using Newtonsoft.Json.Linq;

namespace Shoko.Abstractions.UI;

/// <summary>
/// One value the server offers for an element whose
/// <see cref="UiElement.OptionsRoute"/> is set.
/// </summary>
public class UiOption
{
    /// <summary>
    /// The value, serialised the way the configuration or action itself
    /// serialises it.
    /// </summary>
    public JToken? Value { get; init; }

    /// <summary>
    /// The label to show for the value, or <c>null</c> to show the value itself.
    /// </summary>
    public string? Label { get; init; }
}
