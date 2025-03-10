using System;

namespace Shoko.Plugin.Abstractions.Config.Attributes;

/// <summary>
/// Define to visualize the sections for the class/group in the UI.
/// </summary>
/// <param name="sectionType">The type of section to use for the class/group in the UI.</param>
[AttributeUsage(AttributeTargets.Class)]
public class SectionTypeAttribute(DisplaySectionType sectionType) : Attribute
{
    /// <summary>
    /// The type of section to use for the class/group in the UI.
    /// </summary>
    public DisplaySectionType SectionType { get; } = sectionType;
}
