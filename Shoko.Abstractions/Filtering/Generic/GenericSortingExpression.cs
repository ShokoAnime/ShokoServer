using System;
using Shoko.Abstractions.Filtering.Sorting;

namespace Shoko.Abstractions.Filtering.Generic;

/// <summary>
///   A generic sorting expression for use with <see cref="GenericFilter"/>.
///   Plugins can instantiate this directly with a function to evaluate.
/// </summary>
public sealed class GenericSortingExpression : ISortingExpression
{
    /// <inheritdoc/>
    public bool TimeDependent { get; set; }

    /// <inheritdoc/>
    public bool UserDependent { get; set; }

    /// <inheritdoc/>
    public bool Descending { get; set; }

    /// <inheritdoc/>
    public ISortingExpression? Next { get; set; }

    /// <summary>
    ///   The function to evaluate.
    /// </summary>
    public Func<IFilterableInfo, IFilterableUserInfo?, DateTime?, object?>? Handler { get; set; }

    /// <summary>
    ///   Creates a generic sorting expression.
    /// </summary>
    public GenericSortingExpression() { }

    /// <summary>
    ///   Creates a generic sorting expression with the given evaluate function.
    /// </summary>
    public GenericSortingExpression(
        Func<IFilterableInfo, object?> handler
    )
    {
        Handler = (info, _, _) => handler(info);
    }

    /// <summary>
    ///   Creates a generic sorting expression with the given evaluate function.
    /// </summary>
    public GenericSortingExpression(
        Func<IFilterableInfo, IFilterableUserInfo, object?> handler
    )
    {
        Handler = (info, user, _) => handler(info, user!);
        UserDependent = true;
    }

    /// <summary>
    ///   Creates a generic sorting expression with the given evaluate function.
    /// </summary>
    public GenericSortingExpression(
        Func<IFilterableInfo, IFilterableUserInfo, DateTime, object?> handler
    )
    {
        Handler = (info, user, time) => handler(info, user!, time!.Value);
        TimeDependent = true;
        UserDependent = true;
    }

    /// <summary>
    ///   Creates a generic sorting expression with the given evaluate function.
    /// </summary>
    public GenericSortingExpression(
        Func<IFilterableInfo, IFilterableUserInfo?, DateTime?, object?> handler,
        bool descending,
        bool userDependent,
        bool timeDependent
    )
    {
        Handler = handler;
        Descending = descending;
        TimeDependent = timeDependent;
        UserDependent = userDependent;
    }

    /// <inheritdoc/>
    public object? Evaluate(IFilterableInfo entityInfo, IFilterableUserInfo? userInfo, DateTime? time)
        => Handler is not null ? Handler(entityInfo, userInfo, time) : null;
}
