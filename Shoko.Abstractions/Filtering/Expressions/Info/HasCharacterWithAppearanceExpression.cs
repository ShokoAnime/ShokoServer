using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if the filterable has a character with the specified appearance.
/// </summary>
public class HasCharacterWithAppearanceExpression : FilterExpression<bool>, IWithStringParameter, IWithSecondStringParameter
{
    /// <inheritdoc/>
    public HasCharacterWithAppearanceExpression(string characterID, CastRoleType appearance)
        => (CharacterID, Appearance) = (characterID, appearance);

    /// <inheritdoc/>
    public HasCharacterWithAppearanceExpression() { }

    /// <inheritdoc/>
    protected string? CharacterID { get; set; }

    /// <inheritdoc/>
    protected CastRoleType Appearance { get; set; }

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if the filterable has a character with the specified appearance.";

    string? IWithStringParameter.Parameter
    {
        get => CharacterID;
        set => CharacterID = value;
    }

    string IWithSecondStringParameter.SecondParameter
    {
        get => Appearance.ToString();
        set => Appearance = Enum.Parse<CastRoleType>(value, ignoreCase: true);
    }

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return CharacterID is not null && filterable.CharacterAppearances.TryGetValue(Appearance, out var appearances) && appearances.Contains(CharacterID);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasCharacterWithAppearanceExpression other)
    {
        return base.Equals(other) && CharacterID == other.CharacterID && Appearance == other.Appearance;
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

        return Equals((HasCharacterWithAppearanceExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), CharacterID, (int)Appearance);
    }
}
