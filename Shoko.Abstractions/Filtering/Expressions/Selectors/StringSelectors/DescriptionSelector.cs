using System;

namespace Shoko.Abstractions.Filtering.Expressions.Selectors.StringSelectors;

/// <summary>
/// This returns the description of a filterable
/// </summary>
public class DescriptionSelector : FilterExpression<string>
{
    /// <inheritdoc/>
    public override string HelpDescription => "This returns the description of a filterable";
    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    /// <inheritdoc/>
    public override string Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.Description;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(DescriptionSelector other)
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

        return Equals((DescriptionSelector)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
