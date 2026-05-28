using System;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if any of the anime are missing episodes from a release group that is currently in the collection
/// </summary>
public class HasMissingEpisodesCollectingExpression : FilterExpression<bool>
{
    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the anime are missing episodes from a release group that is currently in the collection";
    /// <inheritdoc/>
    public override string Name => "Has Missing Episodes (Collecting)";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.MissingEpisodesCollecting > 0;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasMissingEpisodesCollectingExpression other)
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

        return Equals((HasMissingEpisodesCollectingExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
