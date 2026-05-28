using System;

namespace Shoko.Abstractions.Filtering.Expressions.Selectors.NumberSelectors;

/// <summary>
/// This returns the number of series with any vote set in a filterable
/// </summary>
public class SeriesVoteCountSelector : FilterExpression<double>
{
    /// <inheritdoc/>
    public override bool UserDependent => true;
    /// <inheritdoc/>
    public override string HelpDescription => "This returns the number of series with any vote set in a filterable";
    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    /// <inheritdoc/>
    public override double Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        return userInfo.SeriesVoteCount;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(SeriesVoteCountSelector other)
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

        return Equals((SeriesVoteCountSelector)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
