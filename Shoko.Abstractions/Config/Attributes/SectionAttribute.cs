using System;
using Shoko.Abstractions.Config.Enums;

namespace Shoko.Abstractions.Config.Attributes;

/// <summary>
/// Define extra details around a section in the UI.
/// </summary>
/// <param name="sectionType">The type of section to use for the class/group in the UI.</param>
[AttributeUsage(AttributeTargets.Class)]
public class SectionAttribute(DisplaySectionType sectionType = DisplaySectionType.FieldSet) : Attribute
{
    /// <summary>
    /// The name of the section to use for the properties/fields not inside a section in the UI.
    /// </summary>
    public string? DefaultSectionName { get; set; }

    /// <summary>
    /// Append any floating sections defined using a <see cref="SectionNameAttribute"/> on
    /// properties/fields at the end of the other section definitions in the UI.
    /// </summary>
    public bool AppendFloatingSectionsAtEnd { get; set; }

    /// <summary>
    /// Show the save action for the class/group in the UI.
    /// </summary>
    public bool ShowSaveAction { get; set; } = false;

    /// <summary>
    /// The type of section to use for the class/group in the UI.
    /// </summary>
    public DisplaySectionType SectionType { get; } = sectionType;
}
