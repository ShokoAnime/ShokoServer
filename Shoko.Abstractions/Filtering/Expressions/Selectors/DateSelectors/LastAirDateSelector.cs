using System;

namespace Shoko.Abstractions.Filtering.Expressions.Selectors.DateSelectors;

/// <summary>
/// This returns the last date that a filterable aired
/// </summary>
public class LastAirDateSelector : FilterExpression<DateTime?>
{
    /// <inheritdoc/>
    public override string HelpDescription => "This returns the last date that a filterable aired";
    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    /// <inheritdoc/>
    public override DateTime? Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.LastAirDate?.ToDateTime();
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(LastAirDateSelector other)
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

        return Equals((LastAirDateSelector)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
