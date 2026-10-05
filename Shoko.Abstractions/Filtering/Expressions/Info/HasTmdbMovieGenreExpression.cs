using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Shoko.Abstractions.Metadata;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if any of the anime have the specified TMDB movie genre
/// </summary>
/// <remarks>
/// The TMDB form of <see cref="HasSourceGenreExpression"/>, narrowed to the
/// linked movies through <see cref="IFilterableInfo.GetGenres"/>. Kept
/// under its own name so saved filters read the same.
/// </remarks>
public class HasTmdbMovieGenreExpression : FilterExpression<bool>, IWithStringParameter
{
    /// <inheritdoc/>
    public HasTmdbMovieGenreExpression(string parameter)
        => Parameter = parameter;

    /// <inheritdoc/>
    public HasTmdbMovieGenreExpression() { }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the anime have the specified TMDB movie genre";

    /// <inheritdoc/>
    public override string HelpParameterName => "Genre";

    /// <inheritdoc/>
    public override string[] HelpPossibleParameters => [];

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return Parameter is not null && filterable.GetGenres(MetadataSource.TMDB, MetadataEntityType.Movie).Contains(Parameter);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasTmdbMovieGenreExpression other)
    {
        return base.Equals(other) && Parameter == other.Parameter;
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

        return Equals((HasTmdbMovieGenreExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Parameter);
}
