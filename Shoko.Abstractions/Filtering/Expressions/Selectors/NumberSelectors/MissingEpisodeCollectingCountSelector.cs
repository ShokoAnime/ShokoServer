using System;

namespace Shoko.Abstractions.Filtering.Expressions.Selectors.NumberSelectors;

/// <summary>
/// This returns the number of missing episodes in a filterable that are from a release group that is already in the filterable
/// </summary>
public class MissingEpisodeCollectingCountSelector : FilterExpression<double>
{

    /// <inheritdoc/>
    public override string HelpDescription => "This returns the number of missing episodes in a filterable that are from a release group that is already in the filterable";
    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    /// <inheritdoc/>
    public override double Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.MissingEpisodesCollecting;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(MissingEpisodeCollectingCountSelector other)
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

        return Equals((MissingEpisodeCollectingCountSelector)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
