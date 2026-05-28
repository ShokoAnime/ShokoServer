using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Filtering.Expressions.Selectors.StringSetSelectors;

/// <summary>
/// This returns a set of all the AniDB IDs in a filterable. Legacy alias for AnidbAnimeIDsSelector.
/// </summary>
// TODO: REMOVE THIS FILTER EXPRESSION SOMETIME IN THE FUTURE AFTER THE LEGACY FILTERS ARE REMOVED!!1!
public class AniDBIDsSelector : FilterExpression<IReadOnlySet<string>>
{
    /// <inheritdoc/>
    public override bool Deprecated => true;

    /// <inheritdoc/>
    public override string HelpDescription => "This returns a set of all the AniDB IDs in a filterable. Legacy alias for AnidbAnimeIDsSelector";

    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    /// <inheritdoc/>
    public override IReadOnlySet<string> Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.AnidbAnimeIDs;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(AniDBIDsSelector other)
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

        return Equals((AniDBIDsSelector)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
