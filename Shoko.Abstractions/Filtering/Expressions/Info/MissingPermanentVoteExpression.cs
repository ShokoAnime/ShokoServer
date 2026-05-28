using System;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
///     Missing Vote Expression
/// </summary>
public class MissingPermanentVoteExpression : FilterExpression<bool>
{
    /// <inheritdoc/>
    public override bool TimeDependent => true;

    /// <inheritdoc/>
    public override bool UserDependent => true;

    /// <inheritdoc/>
    public override string Name => "Missing Permanent Vote";

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if all of the anime are finished but we do not have permanent votes for all of them.";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        return filterable.IsFinished && filterable.SeriesCount != userInfo.SeriesPermanentVoteCount;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(MissingPermanentVoteExpression other)
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

        return Equals((MissingPermanentVoteExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
