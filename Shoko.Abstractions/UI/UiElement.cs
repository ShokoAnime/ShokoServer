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
    /// The events on which editing this element is worth sending to the server,
    /// or an empty list when no live-edit handler watches it.
    /// </summary>
    /// <remarks>
    /// A handler that named no events contributes
    /// <see cref="Config.Enums.ReactiveEventType.All"/>, which stands for any
    /// of them. A handler that named no members watches everything in its
    /// class, and everything below it that has no handler of its own.
    /// </remarks>
    public IReadOnlyList<Config.Enums.ReactiveEventType> ReactsToLiveEdit { get; set; } = [];

    /// <summary>
    /// Actions rendering on the leading edge of this element's row, keyed by
    /// <see cref="UiAction.Name"/> in the containing section container's
    /// <see cref="Elements.UiSectionContainerElement.Actions"/>.
    /// </summary>
    /// <remarks>
    /// An attached action belongs to the element rather than to the order its
    /// container renders in, so it appears here and nowhere else.
    /// </remarks>
    public IReadOnlyList<string> AttachedStartActions { get; set; } = [];

    /// <summary>
    /// Actions rendering on the trailing edge of this element's row, keyed by
    /// <see cref="UiAction.Name"/> in the containing section container's
    /// <see cref="Elements.UiSectionContainerElement.Actions"/>.
    /// </summary>
    /// <remarks>
    /// An action attached without naming an edge lands here, since a button
    /// after the field it acts on is the common case.
    /// </remarks>
    public IReadOnlyList<string> AttachedEndActions { get; set; } = [];

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
