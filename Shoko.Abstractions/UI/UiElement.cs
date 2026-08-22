using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.UI.Enums;

namespace Shoko.Abstractions.UI;

/// <summary>
/// Base class for every node in a <see cref="UiDefinition"/>.
/// </summary>
/// <remarks>
/// The element tree is meant to be self-sufficient for rendering: a client
/// should never need to consult the JSON schema the definition was derived from
/// in order to draw the element or to run a cheap pre-submit check on it.
/// </remarks>
public abstract class UiElement
{
    /// <summary>
    /// Discriminator naming the concrete subclass. Serialised as a plain
    /// property so no type-name handling is needed on either side.
    /// </summary>
    public abstract UiElementKind Kind { get; }

    /// <summary>
    /// The key the element's container files it under, or <c>null</c> when it is
    /// not filed under one — a list's item, a record's key or value.
    /// </summary>
    /// <remarks>
    /// For an element in <see cref="Elements.UiSectionContainerElement.Items"/>
    /// this repeats the map's key, so an element handed around on its own still
    /// knows what it edits.
    /// </remarks>
    public string? Key { get; set; }

    /// <summary>
    /// The human-readable label for the element.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// An optional longer description of the element.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// How much room the element should take up in its container.
    /// </summary>
    public DisplayElementSize Size { get; set; }

    /// <summary>
    /// When and whether the element is shown and editable.
    /// </summary>
    public UiVisibility Visibility { get; set; } = new();

    /// <summary>
    /// An optional badge to render next to the label.
    /// </summary>
    public UiBadge? Badge { get; set; }

    /// <summary>
    /// Whether changing this element requires a server restart to take effect.
    /// </summary>
    public bool RequiresRestart { get; set; }

    /// <summary>
    /// The environment variable backing this element, if any.
    /// </summary>
    public UiEnvironmentVariable? EnvironmentVariable { get; set; }

    /// <summary>
    /// The name of the section within the parent container this element belongs
    /// to, or <c>null</c> to place it in the container's default section.
    /// </summary>
    public string? SectionName { get; set; }

    /// <summary>
    /// The default value for the element, if the schema declared one.
    /// </summary>
    public JToken? Default { get; set; }

    /// <summary>
    /// Whether the parent requires this element to be present.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Whether <c>null</c> is a legal value for this element.
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    /// Values the element must not be set to, or <c>null</c> when unrestricted.
    /// </summary>
    public IReadOnlyList<JToken?>? DeniedValues { get; set; }
}
