using System;
using Shoko.Abstractions.Metadata;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if TMDB suggests anything for the filterable. Looks at what the filterable suggests, not at what suggests it.
/// </summary>
/// <remarks>
/// The TMDB form of <see cref="HasSourceSuggestionExpression"/>, which it
/// evaluates with the tmdb source. Kept under its own name so saved filters
/// read the same.
/// </remarks>
public class HasTmdbSuggestionExpression : FilterExpression<bool>
{
    private static readonly HasSourceSuggestionExpression _tmdb = new(MetadataSource.TMDB.Value);

    /// <inheritdoc/>
    public override string Name => "Has TMDB Suggestion";

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if TMDB suggests anything for the filterable";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return _tmdb.Evaluate(filterable, userInfo, time);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasTmdbSuggestionExpression other)
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

        return Equals((HasTmdbSuggestionExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
