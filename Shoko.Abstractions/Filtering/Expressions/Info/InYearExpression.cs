using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if any of the anime aired in the specified year
/// </summary>
public class InYearExpression : FilterExpression<bool>, IWithNumberParameter
{
    /// <inheritdoc/>
    public InYearExpression(int parameter)
        => Parameter = parameter;

    /// <inheritdoc/>
    public InYearExpression() { }

    /// <inheritdoc/>
    public int Parameter { get; set; }

    /// <inheritdoc/>
    public override bool TimeDependent => true;
    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the anime aired in the specified year";

    double IWithNumberParameter.Parameter
    {
        get => Parameter;
        set => Parameter = (int)value;
    }

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.Years.Contains(Parameter);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(InYearExpression other)
    {
        return base.Equals(other) && Parameter == other.Parameter;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((InYearExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Parameter);
}
