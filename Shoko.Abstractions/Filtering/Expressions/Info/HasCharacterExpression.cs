using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if the filterable has a character.
/// </summary>
public class HasCharacterExpression : FilterExpression<bool>, IWithStringParameter
{
    /// <inheritdoc/>
    public HasCharacterExpression(string characterID)
        => CharacterID = characterID;

    /// <inheritdoc/>
    public HasCharacterExpression() { }

    /// <inheritdoc/>
    protected string? CharacterID { get; set; }

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if the filterable has a character.";

    string? IWithStringParameter.Parameter
    {
        get => CharacterID;
        set => CharacterID = value;
    }

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return CharacterID is not null && filterable.CharacterIDs.Contains(CharacterID);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasCharacterExpression other)
    {
        return base.Equals(other) && CharacterID == other.CharacterID;
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

        return Equals((HasCharacterExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), CharacterID);
}
