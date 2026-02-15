using System;
using Shoko.Abstractions.Config.Enums;

namespace Shoko.Abstractions.Config.Attributes;

/// <summary>
/// Define extra details for a list in the UI.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class ListAttribute : Attribute
{
    /// <summary>
    /// Determines if the add action should be hidden in the UI.
    /// </summary>
    public bool HideAddAction { get; set; }

    /// <summary>
    /// Determines if the remove action should be hidden in the UI.
    /// </summary>
    public bool HideRemoveAction { get; set; }

    private bool? _uniqueItems = null;

    /// <summary>
    /// Determines if the items in the list are unique.
    /// </summary>
    /// <remarks>
    /// Will always be set to true if <see cref="ListType"/> is set to
    /// <see cref="DisplayListType.EnumCheckbox"/>.
    /// </remarks>
    public bool UniqueItems
    {
        get => ListType is DisplayListType.EnumCheckbox || (_uniqueItems ?? ListType is DisplayListType.ComplexDropdown);
        set => _uniqueItems = value;
    }

    private bool? _sortable = null;

    /// <summary>
    /// Determines if the list is sortable in the UI.
    /// </summary>
    public bool Sortable
    {
        get => _sortable ?? ListType is not DisplayListType.ComplexDropdown;
        set => _sortable = value;
    }

    /// <summary>
    /// Determines how to render the list in the UI.
    /// </summary>
    public DisplayListType ListType { get; set; }
}
