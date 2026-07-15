using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Filtering.Expressions.Selectors.StringSetSelectors;

/// <summary>
/// This returns a set of all the TMDB genres (movie and show) in a filterable.
/// </summary>
public class TmdbGenresSelector : FilterExpression<IReadOnlySet<string>>
{

    /// <inheritdoc/>
    public override string HelpDescription => "This returns a set of all the TMDB genres (movie and show) in a filterable.";
    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    /// <inheritdoc/>
    public override IReadOnlySet<string> Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.TmdbGenres;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(TmdbGenresSelector other)
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

        return Equals((TmdbGenresSelector)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
