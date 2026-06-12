using System;
using System.Linq;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
///     This condition passes if any of the anime have missing episodes of the specified type.
/// </summary>
public class HasMissingEpisodeTypeExpression : FilterExpression<bool>, IWithStringParameter
{
    /// <inheritdoc/>
    public HasMissingEpisodeTypeExpression(string parameter)
        => Parameter = parameter;

    /// <inheritdoc/>
    public HasMissingEpisodeTypeExpression() { }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <inheritdoc/>
    protected EpisodeType? EpisodeType => Enum.TryParse<EpisodeType>(Parameter, true, out var type)
        ? type
        : null;

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if the filterable has missing episodes of the specified type";

    /// <inheritdoc/>
    public override string[] HelpPossibleParameters => Enum.GetValues<EpisodeType>().Select(x => x.ToString()).ToArray();

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return EpisodeType.HasValue && filterable.MissingEpisodeCounts[EpisodeType.Value] > 0;
    }

    /// <inheritdoc/>
    protected bool Equals(HasMissingEpisodeTypeExpression other)
    {
        return base.Equals(other) && Parameter == other.Parameter;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((HasMissingEpisodeTypeExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Parameter);
}
