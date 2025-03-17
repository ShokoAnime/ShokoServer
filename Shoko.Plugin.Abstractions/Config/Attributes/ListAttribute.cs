using System;
using Shoko.Plugin.Abstractions.Config.Enums;

namespace Shoko.Plugin.Abstractions.Config.Attributes;

/// <summary>
/// Define extra details for a list in the UI.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class ListAttribute : Attribute
{
    /// <summary>
    /// Determines if default actions for the list should be hidden in the UI.
    /// </summary>
    public bool HideDefaultActions { get; set; }

    private bool? _uniqueItems = null;

    /// <summary>
    /// Determines if the items in the list are unique.
    /// </summary>
    /// <remarks>
    /// Will always be set to true if <see cref="ListType"/> is set to
    /// <see cref="DisplayListType.Checkbox"/>.
    /// </remarks>
    public bool UniqueItems
    {
        get => ListType is DisplayListType.Checkbox || (_uniqueItems ?? ListType is DisplayListType.Dropdown);
        set => _uniqueItems = value;
    }

    private bool? _sortable = null;

    /// <summary>
    /// Determines if the list is sortable in the UI.
    /// </summary>
    public bool Sortable
    {
        get => _sortable ?? ListType is not DisplayListType.Dropdown;
        set => _sortable = value;
    }

    /// <summary>
    /// Determines how to render the list in the UI.
    /// </summary>
    public DisplayListType ListType { get; set; }
}
