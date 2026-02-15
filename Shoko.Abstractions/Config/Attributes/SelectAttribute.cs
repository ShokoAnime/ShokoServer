using System;
using Shoko.Abstractions.Config.Enums;

namespace Shoko.Abstractions.Config.Attributes;

/// <summary>
/// Define extra details for a select in the UI.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class SelectAttribute : Attribute
{
    /// <summary>
    ///   Determines if the select allows multiple items to be selected.
    /// </summary>
    public bool MultipleItems { get; set; }

    /// <summary>
    ///   Determines how to render the select in the UI.
    /// </summary>
    public DisplaySelectType SelectType { get; set; }

    /// <summary>
    ///   Initializes a new instance of the <see cref="SelectAttribute"/> class.
    /// </summary>
    public SelectAttribute() { }

    /// <summary>
    ///   Initializes a new instance of the <see cref="SelectAttribute"/> class.
    /// </summary>
    /// <param name="selectType">
    ///   Determines how to render the select in the UI.
    /// </param>
    public SelectAttribute(DisplaySelectType selectType = DisplaySelectType.Auto)
    {
        SelectType = selectType;
    }
}
