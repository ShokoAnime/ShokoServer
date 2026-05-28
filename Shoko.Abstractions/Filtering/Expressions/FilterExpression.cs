using System;
using Shoko.Abstractions.Extensions;

namespace Shoko.Abstractions.Filtering.Expressions;

/// <summary>
/// Base class for all filter expressions.
/// </summary>
public class FilterExpression : IFilterExpression, IEquatable<FilterExpression>
{
    /// <inheritdoc/>
    [Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
    public virtual bool TimeDependent => false;

    /// <inheritdoc/>
    [Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
    public virtual bool UserDependent => false;

    /// <summary>
    /// Indicates whether this expression is deprecated and should not be used in new filters.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
    public virtual bool Deprecated => false;

    /// <summary>
    /// A human-readable name for this expression, derived from the class name.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
    public virtual string Name =>
        GetType().Name.TrimEnd("Expression").TrimEnd("Function").TrimEnd("Selector").CamelCaseToNatural();

    /// <summary>
    /// The group this expression belongs to, used for categorization in UI.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
    public virtual FilterExpressionGroup Group => FilterExpressionGroup.Info;

    /// <summary>
    /// A description of what this expression does, shown in help tooltips.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
    public virtual string HelpDescription => string.Empty;

    /// <summary>
    /// Valid pairs of first and second parameters for this expression, shown in help.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
    public virtual string[][] HelpPossibleParameterPairs => [];

    /// <summary>
    /// Valid values for the first parameter of this expression, shown in help.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
    public virtual string[] HelpPossibleParameters => [];

    /// <summary>
    /// Valid values for the second parameter of this expression, shown in help.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
    public virtual string[] HelpPossibleSecondParameters => [];

    /// <inheritdoc/>
    public virtual bool Equals(FilterExpression? obj)
        => obj is not null && ReferenceEquals(this, obj);

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is not null && ReferenceEquals(this, obj);

    /// <inheritdoc/>
    public override int GetHashCode()
        => GetType().FullName!.GetHashCode();

    /// <inheritdoc/>
    public virtual bool IsType(FilterExpression? expression)
        => expression is not null && expression.GetType() == GetType();
}

/// <summary>
/// A typed filter expression that returns a value of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of value returned by <see cref="Evaluate"/>.</typeparam>
public abstract class FilterExpression<T> : FilterExpression, IFilterExpression<T>
{
    /// <inheritdoc/>
    public abstract T Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time);
}
