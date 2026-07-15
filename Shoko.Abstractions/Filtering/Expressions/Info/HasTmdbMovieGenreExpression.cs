using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if any of the anime have the specified TMDB movie genre
/// </summary>
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
    public override string[] HelpPossibleParameters => [];

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return Parameter is not null && filterable.TmdbMovieGenres.Contains(Parameter);
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
