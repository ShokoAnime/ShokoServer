using System;
using Shoko.Abstractions.Metadata;

namespace Shoko.Abstractions.Filtering.Expressions.Selectors.NumberSelectors;

/// <summary>
/// This returns the number of suggestions TMDB makes for a filterable. Counts what the filterable suggests, not what suggests it.
/// </summary>
/// <remarks>
/// The TMDB form of <see cref="SourceSuggestionCountSelector"/>, which it
/// evaluates with the tmdb source. Kept under its own name so saved filters
/// read the same.
/// </remarks>
public class TmdbSuggestionCountSelector : FilterExpression<double>
{
    private static readonly SourceSuggestionCountSelector _tmdb = new(MetadataSource.TMDB.Value);

    /// <inheritdoc/>
    public override string HelpDescription => "This returns the number of suggestions TMDB makes for a filterable";

    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    /// <inheritdoc/>
    public override double Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return _tmdb.Evaluate(filterable, userInfo, time);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(TmdbSuggestionCountSelector other)
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

        return Equals((TmdbSuggestionCountSelector)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
