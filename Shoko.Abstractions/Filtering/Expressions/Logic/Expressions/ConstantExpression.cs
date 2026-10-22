using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Logic.Expressions;

/// <summary>
/// This condition passes if its parameter is true. With no parameter it does
/// not take part (see <see cref="FilterExpression.IsApplicable"/>).
/// </summary>
public class ConstantExpression : FilterExpression<bool>, IWithBoolParameter
{
    /// <inheritdoc/>
    public ConstantExpression(bool parameter)
        => Parameter = parameter;

    /// <inheritdoc/>
    public ConstantExpression() { }

    /// <inheritdoc/>
    public bool? Parameter { get; set; }

    /// <inheritdoc/>
    public override bool IsApplicable => Parameter is not null;

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if the parameter is true.";

    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Logic;

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return Parameter ?? false;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(ConstantExpression other)
    {
        return base.Equals(other) && Equals(Parameter, other.Parameter);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;

        if (ReferenceEquals(this, obj))
            return true;

        if (obj.GetType() != GetType())
            return false;

        return Equals((ConstantExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Parameter);

    /// <inheritdoc/>
    public override bool IsType(FilterExpression? expression)
        => expression is ConstantExpression;
}
