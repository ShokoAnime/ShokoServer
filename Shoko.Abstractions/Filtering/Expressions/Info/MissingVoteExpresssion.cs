using System;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
///     Missing Vote Expression
/// </summary>
public class MissingVoteExpression : FilterExpression<bool>
{
    /// <inheritdoc/>
    public override bool UserDependent => true;

    /// <inheritdoc/>
    public override string Name => "Missing Vote";

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if not all of the anime in the filterable have been voted on.";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        return filterable.SeriesCount != userInfo.SeriesVoteCount;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(MissingVoteExpression other)
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

        return Equals((MissingVoteExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
