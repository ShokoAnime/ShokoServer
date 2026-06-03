using System;
using System.Linq;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if the current user has watched any episodes of the specified type
/// </summary>
public class HasWatchedEpisodeTypeExpression : FilterExpression<bool>, IWithStringParameter
{
    /// <inheritdoc/>
    public HasWatchedEpisodeTypeExpression(string parameter)
        => Parameter = parameter;

    /// <inheritdoc/>
    public HasWatchedEpisodeTypeExpression() { }

    /// <inheritdoc/>
    public override bool UserDependent => true;

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <summary>
    /// The parsed episode type from the parameter string.
    /// </summary>
    protected EpisodeType? EpisodeType => Enum.TryParse<EpisodeType>(Parameter, true, out var type)
        ? type
        : null;

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if the current user has watched any episodes of the specified type";

    /// <inheritdoc/>
    public override string[] HelpPossibleParameters => Enum.GetValues<EpisodeType>().Select(x => x.ToString()).ToArray();

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return EpisodeType.HasValue && (userInfo?.WatchedEpisodeCounts[EpisodeType.Value] ?? 0) > 0;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasWatchedEpisodeTypeExpression other)
    {
        return base.Equals(other) && Parameter == other.Parameter;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((HasWatchedEpisodeTypeExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Parameter);
}
