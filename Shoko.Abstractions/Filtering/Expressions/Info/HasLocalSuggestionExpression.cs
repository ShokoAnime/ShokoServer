using System;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if the filterable suggests a series in the collection. Looks at what the filterable suggests, not at what suggests it.
/// </summary>
public class HasLocalSuggestionExpression : FilterExpression<bool>
{
    /// <inheritdoc/>
    public override string Name => "Has Local Suggestion";

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if the filterable suggests a series in the collection";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.LocalSuggestions is > 0;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasLocalSuggestionExpression other)
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

        return Equals((HasLocalSuggestionExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
