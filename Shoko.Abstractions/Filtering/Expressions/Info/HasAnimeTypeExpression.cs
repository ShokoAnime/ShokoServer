using System;
using System.Linq;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if any of the anime are of the specified type
/// </summary>
public class HasAnimeTypeExpression : FilterExpression<bool>, IWithStringParameter
{
    /// <inheritdoc/>
    public HasAnimeTypeExpression(string parameter)
        => Parameter = parameter;

    /// <inheritdoc/>
    public HasAnimeTypeExpression() { }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <summary>
    /// The parsed anime type from the parameter string.
    /// </summary>
    protected AnimeType? AnimeType => Enum.TryParse<AnimeType>(Parameter, true, out var animeType)
        ? animeType
        : null;

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the anime are of the specified type";

    /// <inheritdoc/>
    public override string[] HelpPossibleParameters => Enum.GetValues<AnimeType>().Select(x => x.ToString()).ToArray();

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return AnimeType.HasValue && filterable.AnimeTypes.Contains(AnimeType.Value);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasAnimeTypeExpression other)
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

        return Equals((HasAnimeTypeExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Parameter);
}
