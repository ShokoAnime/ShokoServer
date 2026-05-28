using System;

namespace Shoko.Abstractions.Filtering.Expressions.Selectors.StringSelectors;

/// <summary>
/// This returns the name of a filterable
/// </summary>
public class NameSelector : FilterExpression<string>
{
    /// <inheritdoc/>
    public override string HelpDescription => "This returns the name of a filterable";
    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    /// <inheritdoc/>
    public override string Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.Name;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(NameSelector other)
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

        return Equals((NameSelector)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
