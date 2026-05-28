using System;

namespace Shoko.Abstractions.Filtering.Expressions.Selectors.DateSelectors;

/// <summary>
/// This returns the last date that any episode was added in a filterable
/// </summary>
public class LastAddedDateSelector : FilterExpression<DateTime?>
{
    /// <inheritdoc/>
    public override string HelpDescription => "This returns the last date that any episode was added in a filterable";
    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    /// <inheritdoc/>
    public override DateTime? Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.LastAddedDate;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(LastAddedDateSelector other)
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

        return Equals((LastAddedDateSelector)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
