using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if the filterable has a creator.
/// </summary>
public class HasCreatorExpression : FilterExpression<bool>, IWithStringParameter
{
    /// <inheritdoc/>
    public HasCreatorExpression(string creatorID)
        => CreatorID = creatorID;

    /// <inheritdoc/>
    public HasCreatorExpression() { }

    /// <inheritdoc/>
    protected string? CreatorID { get; set; }

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if the filterable has a creator.";

    string? IWithStringParameter.Parameter
    {
        get => CreatorID;
        set => CreatorID = value;
    }

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return CreatorID is not null && filterable.CreatorIDs.Contains(CreatorID);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasCreatorExpression other)
    {
        return base.Equals(other) && CreatorID == other.CreatorID;
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

        return Equals((HasCreatorExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), CreatorID);
}
