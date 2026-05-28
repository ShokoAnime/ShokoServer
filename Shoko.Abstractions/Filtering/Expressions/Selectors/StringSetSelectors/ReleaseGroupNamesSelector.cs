using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Filtering.Expressions.Selectors.StringSetSelectors;

/// <summary>
/// This returns a set of all release group names in a filterable.
/// </summary>
public class ReleaseGroupNamesSelector : FilterExpression<IReadOnlySet<string>>
{
    /// <inheritdoc/>
    public override bool TimeDependent => false;

    /// <inheritdoc/>
    public override bool UserDependent => false;

    /// <inheritdoc/>
    public override string HelpDescription => "This returns a set of all release group names in a filterable.";

    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    /// <inheritdoc/>
    public override IReadOnlySet<string> Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? now)
    {
        return filterable.ReleaseGroupNames;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(ReleaseGroupNamesSelector other)
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

        return Equals((ReleaseGroupNamesSelector)obj);
    }
    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
