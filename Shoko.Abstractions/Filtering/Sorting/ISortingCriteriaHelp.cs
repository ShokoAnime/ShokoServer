using System;

namespace Shoko.Abstractions.Filtering.Sorting;

/// <summary>
///   Describes a sorting expression available for use in filter presets.
/// </summary>
public interface ISortingCriteriaHelp
{
    /// <summary>
    ///   The implementation type of the SortingExpression.
    /// </summary>
    Type InternalType { get; }

    /// <summary>
    ///   The internal type name of the SortingExpression.
    ///   This is what you give the API, not actually the internal type.
    /// </summary>
    string Type { get; }

    /// <summary>
    ///   Human readable name.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///   A description of what the sorting expression is doing.
    /// </summary>
    string Description { get; }
}
