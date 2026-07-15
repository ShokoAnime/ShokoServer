using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if any of the anime have the specified TMDB genre
/// </summary>
public class HasTmdbGenreExpression : FilterExpression<bool>, IWithStringParameter
{
    /// <inheritdoc/>
    public HasTmdbGenreExpression(string parameter)
        => Parameter = parameter;

    /// <inheritdoc/>
    public HasTmdbGenreExpression() { }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the anime have the specified TMDB genre";

    /// <inheritdoc/>
    public override string[] HelpPossibleParameters => [];

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return Parameter is not null && filterable.TmdbGenres.Contains(Parameter);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasTmdbGenreExpression other)
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

        return Equals((HasTmdbGenreExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Parameter);
}
