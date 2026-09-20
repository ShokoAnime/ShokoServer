using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.UI;

/// <summary>
/// A self-sufficient description of how to render an editor for a set of
/// values, derived from the values' JSON schema and their authoring
/// attributes.
/// </summary>
/// <remarks>
/// <para>
/// A client should be able to render the whole editor, and run the cheap
/// pre-submit constraint checks, from this object alone. The JSON schema
/// remains the authority for server-side validation.
/// </para>
/// <para>
/// Nothing here is specific to what owns the definition, so the same shape
/// describes a configuration and an executable action's invocation parameters
/// alike, and a client renders both the same way.
/// </para>
/// </remarks>
public class UiDefinition
{
    /// <summary>
    /// The id of whatever this definition describes — a configuration's id, or
    /// an executable action's id when it describes that action's parameters.
    /// </summary>
    public Guid ID { get; init; }

    /// <summary>
    /// The display name of whatever this definition describes.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// An optional longer description of whatever this definition describes.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The root element of the editor.
    /// </summary>
    public UiElement Root { get; init; } = null!;

    /// <summary>
    /// Elements hoisted out of <see cref="Root"/> because inlining them would
    /// have recursed forever. Keyed by the name a
    /// <see cref="Elements.UiReferenceElement"/> points at.
    /// </summary>
    public IReadOnlyDictionary<string, UiElement> Definitions { get; init; } = new Dictionary<string, UiElement>();
}
