using System;
using System.Runtime.CompilerServices;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Attributes;

/// <summary>
/// An attribute for defining a renamer setting on a renamer settings object.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class RenamerSettingAttribute : Attribute
{
    /// <summary>
    /// The name of the setting to be displayed
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The type of the setting, to be used in the UI
    /// </summary>
    public RenamerSettingType Type { get; set; }

    /// <summary>
    /// The language to use for text highlighting in the editor
    /// </summary>
    public CodeLanguage Language { get; set; }

    /// <summary>
    /// The description of the setting and what it controls
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Create a new setting definition for a property.
    /// </summary>
    /// <param name="name">The name of the setting to be displayed. Will be inferred if not specified.</param>
    /// <param name="type">The type of the setting, to be used in the UI. Will be inferred if not specified.</param>
    /// <param name="description">The description of the setting and what it controls. Can be omitted.</param>
    public RenamerSettingAttribute([CallerMemberName] string? name = null, RenamerSettingType type = RenamerSettingType.Auto, string? description = null)
    {
        // the nullability is suppressed because [CallerMemberName] is used
        Name = name!;
        Type = type;
        Description = description;
    }
}
