using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if any of the anime have the specified AniDB tag by ID
/// </summary>
public class HasTagByIDExpression : FilterExpression<bool>, IWithStringParameter
{
    /// <inheritdoc/>
    public HasTagByIDExpression(string parameter)
        => Parameter = parameter;

    /// <inheritdoc/>
    public HasTagByIDExpression() { }

    /// <inheritdoc/>
    public string? Parameter { get; set; }
    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the anime have the specified AniDB tag by ID";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return Parameter is not null && filterable.AnidbTagIDs.Contains(Parameter);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasTagByIDExpression other)
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

        return Equals((HasTagByIDExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Parameter);
}
