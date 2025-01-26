using System;
using Shoko.Server.Filters.Interfaces;
using Shoko.Server.Server;

namespace Shoko.Server.Filters.Info;

public class HasCharacterWithAppearanceExpression : FilterExpression<bool>, IWithStringParameter, IWithSecondStringParameter
{
    public HasCharacterWithAppearanceExpression(string characterID, CharacterAppearanceType appearance)
    {
        CharacterID = characterID;
        Appearance = appearance;
    }

    public HasCharacterWithAppearanceExpression() { }

    public string CharacterID { get; set; }
    public CharacterAppearanceType Appearance { get; set; }
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This condition passes if the filterable has a character with the specified appearance.";

    string IWithStringParameter.Parameter
    {
        get => CharacterID;
        set => CharacterID = value;
    }

    string IWithSecondStringParameter.SecondParameter
    {
        get => Appearance.ToString();
        set => Appearance = Enum.Parse<CharacterAppearanceType>(value);
    }

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
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
