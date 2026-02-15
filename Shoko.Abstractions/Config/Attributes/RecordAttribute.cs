using System;
using Shoko.Abstractions.Config.Enums;

namespace Shoko.Abstractions.Config.Attributes;

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

    private bool? _sortable = null;

    /// <summary>
    /// Determines if the record is sortable in the UI.
    /// </summary>
    public bool Sortable
    {
        get => _sortable ?? RecordType is not DisplayRecordType.ComplexDropdown;
        set => _sortable = value;
    }

    /// <summary>
    /// Determines how to render the record in the UI.
    /// </summary>
    public DisplayRecordType RecordType { get; set; }
}
