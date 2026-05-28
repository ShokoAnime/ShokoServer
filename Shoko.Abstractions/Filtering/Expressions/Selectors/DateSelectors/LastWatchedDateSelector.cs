using System;

namespace Shoko.Abstractions.Filtering.Expressions.Selectors.DateSelectors;

/// <summary>
/// This returns the last date that a filterable was watched by the current user
/// </summary>
public class LastWatchedDateSelector : FilterExpression<DateTime?>
{
    /// <inheritdoc/>
    public override bool UserDependent => true;
    /// <inheritdoc/>
    public override string HelpDescription => "This returns the last date that a filterable was watched by the current user";
    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    /// <inheritdoc/>
    public override DateTime? Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        return userInfo.LastWatchedDate;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(LastWatchedDateSelector other)
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

        return Equals((LastWatchedDateSelector)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
