using System;
using Shoko.Abstractions.Filtering.Services;

namespace Shoko.Abstractions.Filtering;

/// <summary>
///   Base interface for all filter expressions.
/// </summary>
public interface IFilterExpression
{
    /// <summary>
    ///   Indicates the expression is time dependent, meaning the current time
    ///   may change the result of the evaluation.
    /// </summary>
    bool TimeDependent { get; }

    /// <summary>
    ///   Indicates the expression is user dependent, meaning a user must be
    ///   provided to the <see cref="IFilterEvaluator"/> in order to evaluate
    ///   it.
    /// </summary>
    bool UserDependent { get; }
}

/// <summary>
///   A filter expression that returns a value.
/// </summary>
/// <typeparam name="T">
///   The type of the value returned by the expression.
/// </typeparam>
public interface IFilterExpression<out T> : IFilterExpression
{
    /// <summary>
    ///   Evaluates the expression, returning the result.
    /// </summary>
    /// <param name="entityInfo">
    ///   Information about the entity being evaluated.
    /// </param>
    /// <param name="userInfo">
    ///   User-specific information about the entity being evaluated.
    /// </param>
    /// <param name="time">
    ///   The date and time the evaluation is evaluated for.
    /// </param>
    /// <returns>
    ///   The result of the evaluation.
    /// </returns>
    T Evaluate(IFilterableInfo entityInfo, IFilterableUserInfo? userInfo, DateTime? time);
}
