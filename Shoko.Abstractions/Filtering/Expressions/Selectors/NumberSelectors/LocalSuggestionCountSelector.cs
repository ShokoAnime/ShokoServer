using System;

namespace Shoko.Abstractions.Filtering.Expressions.Selectors.NumberSelectors;

/// <summary>
/// This returns the number of suggestions a filterable makes that point at a series in the collection. Counts what the filterable suggests, not what suggests it.
/// </summary>
public class LocalSuggestionCountSelector : FilterExpression<double>
{
    /// <inheritdoc/>
    public override string HelpDescription => "This returns the number of suggestions a filterable makes that point at a series in the collection";

    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    /// <inheritdoc/>
    public override double Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.LocalSuggestions;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(LocalSuggestionCountSelector other)
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

        return Equals((LocalSuggestionCountSelector)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
