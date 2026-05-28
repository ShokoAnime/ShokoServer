using System;

namespace Shoko.Abstractions.Filtering.Expressions.Selectors.DateSelectors;

/// <summary>
/// This returns the date that a filterable was created
/// </summary>
public class AddedDateSelector : FilterExpression<DateTime?>
{
    /// <inheritdoc/>
    public override string HelpDescription => "This returns the date that a filterable was created";

    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    /// <inheritdoc/>
    public override DateTime? Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.AddedDate;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(AddedDateSelector other)
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

        return Equals((AddedDateSelector)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
