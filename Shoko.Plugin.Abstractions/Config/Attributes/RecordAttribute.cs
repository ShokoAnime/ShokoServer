using System;
using Shoko.Plugin.Abstractions.Config.Enums;

namespace Shoko.Plugin.Abstractions.Config.Attributes;

/// <summary>
/// Define extra details for a record in the UI.
/// /// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class RecordAttribute : Attribute
{
    /// <summary>
    /// Determines if the add action should be hidden in the UI.
    /// </summary>
    public bool HideAddAction { get; set; }

    /// <summary>
    /// Determines if the remove action should be hidden in the UI.
    /// </summary>
    public bool HideRemoveAction { get; set; }
}
