using System;

namespace Shoko.Abstractions.UI.Attributes;

/// <summary>
/// Describe a section the class's members are gathered into with a
/// <see cref="SectionNameAttribute"/>.
/// </summary>
/// <remarks>
/// A gathered section is not a type of its own, so it has nowhere to carry a
/// description; this is that place. Declare one per section, next to the
/// <see cref="SectionAttribute"/> — a member still names the section it belongs
/// to, and this only describes the section it names.
/// <para>
/// Sections order by the first member in them either way, so declaring one here
/// does not move it, and declaring a name no member uses leaves nothing behind.
/// </para>
/// </remarks>
/// <param name="name">
/// The name of the section, as the members of it spell it.
/// </param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class FloatingSectionAttribute(string name) : Attribute
{
    /// <summary>
    /// The name of the section, as the members of it spell it.
    /// </summary>
    public string Name { get; init; } = name;

    /// <summary>
    /// A longer description of the section, rendered under its title.
    /// </summary>
    public string? Description { get; set; }
}
