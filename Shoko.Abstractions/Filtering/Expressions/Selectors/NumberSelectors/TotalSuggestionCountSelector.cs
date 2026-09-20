using System;

namespace Shoko.Abstractions.Filtering.Expressions.Selectors.NumberSelectors;

/// <summary>
/// This returns the number of suggestions all the sources make for a filterable. Counts what the filterable suggests, not what suggests it.
/// </summary>
public class TotalSuggestionCountSelector : FilterExpression<double>
{
    /// <inheritdoc/>
    public override string HelpDescription => "This returns the number of suggestions all the sources make for a filterable";

    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    /// <inheritdoc/>
    public override double Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.TotalSuggestions;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(TotalSuggestionCountSelector other)
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

        return Equals((TotalSuggestionCountSelector)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
