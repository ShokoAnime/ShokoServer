using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Shoko.Abstractions.Metadata;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if any of the anime have the specified TMDB keyword
/// </summary>
/// <remarks>
/// The TMDB form of <see cref="HasSourceTagExpression"/>, which it
/// evaluates with the tmdb source and the parameter as the name. Kept under
/// its own name so saved filters read the same.
/// </remarks>
public class HasTmdbKeywordExpression : FilterExpression<bool>, IWithStringParameter
{
    /// <inheritdoc/>
    public HasTmdbKeywordExpression(string parameter)
        => Parameter = parameter;

    /// <inheritdoc/>
    public HasTmdbKeywordExpression() { }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the anime have the specified TMDB keyword";

    /// <inheritdoc/>
    public override string[] HelpPossibleParameters => [];

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return Parameter is not null && Tmdb(Parameter).Evaluate(filterable, userInfo, time);
    }

    private HasSourceTagExpression? _tmdb;

    /// <summary>
    /// The generic expression asking the same of the tmdb source, made again
    /// only when the parameter changes.
    /// </summary>
    /// <param name="parameter">The name to look for.</param>
    /// <returns>The expression.</returns>
    private HasSourceTagExpression Tmdb(string parameter)
        => _tmdb is { } tmdb && tmdb.SecondParameter == parameter ? tmdb : _tmdb = new(MetadataSource.TMDB.Value, parameter);

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasTmdbKeywordExpression other)
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

        return Equals((HasTmdbKeywordExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Parameter);
}
