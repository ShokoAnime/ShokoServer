using System;

namespace Shoko.Abstractions.Filtering.Expressions.Selectors.NumberSelectors;

/// <summary>
/// This returns the number of suggestions AniList makes for a filterable. Counts what the filterable suggests, not what suggests it.
/// </summary>
public class AnilistSuggestionCountSelector : FilterExpression<double>
{
    /// <inheritdoc/>
    public override string HelpDescription => "This returns the number of suggestions AniList makes for a filterable";

    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    /// <inheritdoc/>
    public override double Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.AnilistSuggestions;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(AnilistSuggestionCountSelector other)
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

        return Equals((AnilistSuggestionCountSelector)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
