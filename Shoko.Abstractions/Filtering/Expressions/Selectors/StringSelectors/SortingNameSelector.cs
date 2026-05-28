using System;

namespace Shoko.Abstractions.Filtering.Expressions.Selectors.StringSelectors;

/// <summary>
///   This returns the sorting name of a filterable. Legacy alias for SortNameSelector.
/// </summary>
// TODO: REMOVE THIS FILTER EXPRESSION SOMETIME IN THE FUTURE AFTER THE LEGACY FILTERS ARE REMOVED!!1!
public class SortingNameSelector : FilterExpression<string>
{
    /// <inheritdoc/>
    public override bool Deprecated => true;

    /// <inheritdoc/>
    public override string HelpDescription => "This returns the sorting name of a filterable. Legacy alias for SortNameSelector.";
    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    /// <inheritdoc/>
    public override string Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.SortName;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(SortingNameSelector other)
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

        return Equals((SortingNameSelector)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
