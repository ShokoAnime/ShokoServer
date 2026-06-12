using System;

namespace Shoko.Abstractions.Filtering.Expressions.Selectors.NumberSelectors;

/// <summary>
/// This returns the number of unaired other episodes in a filterable
/// </summary>
public class UnairedOthersEpisodesCountSelector : FilterExpression<double>
{
    /// <inheritdoc/>
    public override string HelpDescription => "This returns the number of unaired other episodes in a filterable";

    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    /// <inheritdoc/>
    public override double Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.UnairedEpisodeCounts.Others;
    }

    /// <inheritdoc/>
    protected bool Equals(UnairedOthersEpisodesCountSelector other)
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

        return Equals((UnairedOthersEpisodesCountSelector)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
