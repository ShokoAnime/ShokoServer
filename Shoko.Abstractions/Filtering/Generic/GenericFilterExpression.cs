using System;
using Shoko.Abstractions.Filtering.Expressions;

namespace Shoko.Abstractions.Filtering.Generic;

/// <summary>
///   A generic filter expression for use with <see cref="GenericFilter"/>.
///   Plugins can instantiate this directly with a function to evaluate.
/// </summary>
public sealed class GenericFilterExpression : IFilterExpression<bool>
{
    /// <inheritdoc/>
    public bool TimeDependent { get; set; }

    /// <inheritdoc/>
    public bool UserDependent { get; set; }

    /// <summary>
    ///   The function to evaluate.
    /// </summary>
    public Func<IFilterableInfo, IFilterableUserInfo?, DateTime?, bool>? Handler { get; set; }

    /// <summary>
    ///   Creates a generic filter expression.
    /// </summary>
    public GenericFilterExpression() { }

    /// <summary>
    ///   Creates a generic filter expression with the given evaluate function.
    /// </summary>
    public GenericFilterExpression(
        Func<IFilterableInfo, bool> handler
    )
    {
        Handler = (info, _, _) => handler(info);
    }

    /// <summary>
    ///   Creates a generic filter expression with the given evaluate function.
    /// </summary>
    public GenericFilterExpression(
        Func<IFilterableInfo, IFilterableUserInfo, bool> handler
    )
    {
        Handler = (info, user, _) => handler(info, user!);
        UserDependent = true;
    }

    /// <summary>
    ///   Creates a generic filter expression with the given evaluate function.
    /// </summary>
    public GenericFilterExpression(
        Func<IFilterableInfo, IFilterableUserInfo, DateTime, bool> handler
    )
    {
        Handler = (info, user, time) => handler(info, user!, time!.Value);
        TimeDependent = true;
        UserDependent = true;
    }

    /// <summary>
    ///   Creates a generic filter expression with the given evaluate function.
    /// </summary>
    public GenericFilterExpression(
        Func<IFilterableInfo, IFilterableUserInfo?, DateTime?, bool> handler,
        bool userDependent,
        bool timeDependent
    )
    {
        Handler = handler;
        TimeDependent = timeDependent;
        UserDependent = userDependent;
    }

    /// <inheritdoc/>
    public bool Evaluate(IFilterableInfo entityInfo, IFilterableUserInfo? userInfo, DateTime? time)
        => Handler is not null && Handler(entityInfo, userInfo, time);
}
