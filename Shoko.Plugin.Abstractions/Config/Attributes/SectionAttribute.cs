using System;
using Shoko.Plugin.Abstractions.Config.Enums;

namespace Shoko.Plugin.Abstractions.Config.Attributes;

/// <summary>
/// Define extra details around a section in the UI.
/// </summary>
/// <param name="sectionType">The type of section to use for the class/group in the UI.</param>
[AttributeUsage(AttributeTargets.Class)]
public class SectionAttribute(DisplaySectionType sectionType) : Attribute
{
    /// <summary>
    /// The type of section to use for the class/group in the UI.
    /// </summary>
    public DisplaySectionType SectionType { get; } = sectionType;
}
