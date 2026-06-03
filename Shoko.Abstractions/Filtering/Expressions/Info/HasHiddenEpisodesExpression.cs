using System;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if the filterable has any hidden episodes
/// </summary>
public class HasHiddenEpisodesExpression : FilterExpression<bool>
{
    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if the filterable has any hidden episodes";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.HiddenEpisodes > 0;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasHiddenEpisodesExpression other)
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

        return Equals((HasHiddenEpisodesExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
