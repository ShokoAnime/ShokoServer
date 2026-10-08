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
    /// not filed under one, such as a list's item or a record's key or value.
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
    /// Where to fetch the values the element may take, or <c>null</c> when the
    /// server does not list them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// POST to it with the member's path as the <c>path</c> query parameter,
    /// the same path a custom action is invoked with, and the edited document
    /// as the body. The path is always the member's own, so an entry of a list
    /// or a key or value of a dictionary, which carries the route itself, is
    /// listed for by its member's path. A key element's route ends in
    /// <c>/Keys</c>. A scoped action's route holds the entity's placeholder,
    /// such as <c>{seriesID}</c>, for the client to fill in.
    /// </para>
    /// <para>
    /// The answer is a list of <see cref="UiOption"/> in the provider's order,
    /// possibly empty, or a validation problem keyed by member path when the
    /// provider refused the draft.
    /// </para>
    /// </remarks>
    public string? OptionsRoute { get; set; }

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
    /// <remarks>
    /// Only an element holding a value of its own carries these. Authored on a
    /// collection they describe an entry, so they are filed on the item; an
    /// element with no value to match, a container or a select whose options
    /// live in the configuration value, carries none.
    /// </remarks>
    public IReadOnlyList<JToken?>? DeniedValues { get; set; }
}
