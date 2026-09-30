using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata;

namespace Shoko.Abstractions.Filtering.Expressions.Selectors.StringSetSelectors;

/// <summary>
/// This returns a set of all the TMDB movie genres in a filterable.
/// </summary>
/// <remarks>
/// The TMDB form of <see cref="SourceGenresSelector"/>, narrowed to the
/// linked movies through <see cref="IFilterableInfo.GetGenres"/>. Kept
/// under its own name so saved filters read the same.
/// </remarks>
public class TmdbMovieGenresSelector : FilterExpression<IReadOnlySet<string>>
{

    /// <inheritdoc/>
    public override string HelpDescription => "This returns a set of all the TMDB movie genres in a filterable.";
    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    /// <inheritdoc/>
    public override IReadOnlySet<string> Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.GetGenres(MetadataSource.TMDB, MetadataEntityType.Movie);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(TmdbMovieGenresSelector other)
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

        return Equals((TmdbMovieGenresSelector)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
