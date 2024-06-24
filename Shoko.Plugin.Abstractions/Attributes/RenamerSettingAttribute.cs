using System;
using System.Runtime.CompilerServices;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Attributes;

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
    /// The description of the setting and what it controls
    /// </summary>
    public string? Description { get; set; }

    
    public RenamerSettingAttribute([CallerMemberName] string? name = null, RenamerSettingType type = RenamerSettingType.Auto, string? description = null)
    {
        // the nullability is suppressed because [CallerMemberName] is used
        Name = name!;
        Type = type;
        Description = description;
    }
}
