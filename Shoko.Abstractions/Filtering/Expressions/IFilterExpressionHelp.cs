using System;

namespace Shoko.Abstractions.Filtering.Expressions;

/// <summary>
///   Describes a filter expression available for use in filter presets.
/// </summary>
public interface IFilterExpressionHelp
{
    /// <summary>
    ///   The implementation type of the FilterExpression.
    /// </summary>
    Type InternalType { get; }

    /// <summary>
    ///   The internal type name of the FilterExpression.
    ///   This is what you give the API, not actually the internal type.
    /// </summary>
    string Expression { get; }

    /// <summary>
    ///   The human readable name of the Expression.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///   The group that this filter expression belongs to. This can help
    ///   with filtering the expression types.
    /// </summary>
    FilterExpressionGroup Group { get; }

    /// <summary>
    ///   A description of what the expression is doing, comparing, etc.
    /// </summary>
    string Description { get; }

    /// <summary>
    ///   What the expression would be considered for parameters.
    ///   For example, Air Date is a Date Selector.
    /// </summary>
    FilterExpressionParameterType Type { get; }

    /// <summary>
    ///   The parameter type that the Left property requires.
    /// </summary>
    FilterExpressionParameterType? Left { get; }

    /// <summary>
    ///   The parameter type that the Right property requires.
    ///   If multiple are given, then at least one is required.
    /// </summary>
    FilterExpressionParameterType? Right { get; }

    /// <summary>
    ///   The parameter type that the Parameter property requires.
    ///   This will always be a string for simplicity in type safety,
    ///   but the type is what it expects.
    /// </summary>
    FilterExpressionParameterType? Parameter { get; }

    /// <summary>
    ///   The parameter type that the SecondParameter property requires.
    ///   This will always be a string for simplicity in type safety,
    ///   but the type is what it expects.
    /// </summary>
    FilterExpressionParameterType? SecondParameter { get; }

    /// <summary>
    ///   This will list the possible parameters, usually with the most
    ///   common ones first.
    /// </summary>
    string[]? PossibleParameters { get; }

    /// <summary>
    ///   This will list the possible second parameters, usually with the
    ///   most common ones first.
    /// </summary>
    string[]? PossibleSecondParameters { get; }

    /// <summary>
    ///   This will list the possible parameter pairs, usually with the
    ///   most common ones first.
    /// </summary>
    string[][]? PossibleParameterPairs { get; }
}
