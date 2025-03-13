using System;
using Shoko.Plugin.Abstractions.Config.Enums;

namespace Shoko.Plugin.Abstractions.Config.Attributes;

/// <summary>
/// 
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class ListAttribute : Attribute
{
    /// <summary>
    /// Determines if default actions for the list are displayed in the UI.
    /// </summary>
    public bool DisplayDefaultActions { get; set; }

    private bool? _uniqueItems = null;

    /// <summary>
    /// Determines if the items in the list are unique.
    /// </summary>
    public bool UniqueItems
    {
        get => _uniqueItems ?? ListType is not DisplayListType.Auto;
        set => _uniqueItems = value;
    }

    /// <summary>
    /// Determines if the list is sortable in the UI.
    /// </summary>
    public bool Sortable { get; set; } = true;

    /// <summary>
    /// Determines how to render the list in the UI.
    /// </summary>
    public DisplayListType ListType { get; set; }
}
