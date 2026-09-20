using System;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if AniDB suggests anything for the filterable. Looks at what the filterable suggests, not at what suggests it.
/// </summary>
public class HasAnidbSuggestionExpression : FilterExpression<bool>
{
    /// <inheritdoc/>
    public override string Name => "Has AniDB Suggestion";

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if AniDB suggests anything for the filterable";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.AnidbSuggestions is > 0;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasAnidbSuggestionExpression other)
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

        return Equals((HasAnidbSuggestionExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
