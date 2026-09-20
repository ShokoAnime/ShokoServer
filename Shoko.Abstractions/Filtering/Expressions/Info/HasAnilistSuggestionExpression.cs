using System;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if AniList suggests anything for the filterable. Looks at what the filterable suggests, not at what suggests it.
/// </summary>
public class HasAnilistSuggestionExpression : FilterExpression<bool>
{
    /// <inheritdoc/>
    public override string Name => "Has AniList Suggestion";

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if AniList suggests anything for the filterable";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.AnilistSuggestions is > 0;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasAnilistSuggestionExpression other)
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

        return Equals((HasAnilistSuggestionExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
