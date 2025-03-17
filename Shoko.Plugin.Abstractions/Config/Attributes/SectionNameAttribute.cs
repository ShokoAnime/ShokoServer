
using System;

namespace Shoko.Plugin.Abstractions.Config.Attributes;

/// <summary>
/// Define the name of a section in the UI.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class SectionNameAttribute(string name) : Attribute
{
    /// <summary>
    /// The name of the section to place the property/field under in the UI.
    /// </summary>
    public string Name { get; init; } = name;
}
