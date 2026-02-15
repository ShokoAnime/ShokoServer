using System;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Filtering;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Info;

public class HasCharacterWithAppearanceExpression : FilterExpression<bool>, IWithStringParameter, IWithSecondStringParameter
{
    public HasCharacterWithAppearanceExpression(string characterID, CastRoleType appearance)
    {
        CharacterID = characterID;
        Appearance = appearance;
    }

    public HasCharacterWithAppearanceExpression() { }

    public string CharacterID { get; set; }

    public CastRoleType Appearance { get; set; }

    public override string HelpDescription => "This condition passes if the filterable has a character with the specified appearance.";

    string IWithStringParameter.Parameter
    {
        get => CharacterID;
        set => CharacterID = value;
    }

    string IWithSecondStringParameter.SecondParameter
    {
        get => Appearance.ToString();
        set => Appearance = Enum.Parse<CastRoleType>(value, ignoreCase: true);
    }

    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.CharacterAppearances.TryGetValue(Appearance, out var appearances) && appearances.Contains(CharacterID);
    }

    protected bool Equals(HasCharacterWithAppearanceExpression other)
    {
        return base.Equals(other) && CharacterID == other.CharacterID && Appearance == other.Appearance;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != this.GetType())
        {
            return false;
        }

        return Equals((HasCharacterWithAppearanceExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), CharacterID, (int)Appearance);
    }

    public static bool operator ==(HasCharacterWithAppearanceExpression left, HasCharacterWithAppearanceExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasCharacterWithAppearanceExpression left, HasCharacterWithAppearanceExpression right)
    {
        return !Equals(left, right);
    }
}
