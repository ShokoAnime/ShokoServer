using System;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if any of the anime are restricted
/// </summary>
public class IsRestrictedExpression : FilterExpression<bool>
{
    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the anime are restricted";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.IsRestricted;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(IsRestrictedExpression other)
    {
        return base.Equals(other);
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

        return Equals((IsRestrictedExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
