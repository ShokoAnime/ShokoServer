using System;
using Shoko.Abstractions.Metadata;

namespace Shoko.Abstractions.Filtering.Expressions.Selectors.NumberSelectors;

/// <summary>
/// This returns the number of automatic TMDB episode links for a series
/// </summary>
/// <remarks>
/// The TMDB form of <see cref="AutomaticSourceEpisodeLinksSelector"/>,
/// which it evaluates with the tmdb source. Kept under its own name so
/// saved filters read the same.
/// </remarks>
public class AutomaticTmdbEpisodeLinksSelector : FilterExpression<double>
{
    private static readonly AutomaticSourceEpisodeLinksSelector _tmdb = new(MetadataSource.TMDB.Value);

    /// <inheritdoc/>
    public override string HelpDescription => "This returns the number of automatic TMDB episode links for a series";
    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    /// <inheritdoc/>
    public override double Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return _tmdb.Evaluate(filterable, userInfo, time);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(AutomaticTmdbEpisodeLinksSelector other)
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

        return Equals((AutomaticTmdbEpisodeLinksSelector)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
