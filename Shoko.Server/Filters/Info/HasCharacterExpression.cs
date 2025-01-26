using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Info;

public class HasCharacterExpression : FilterExpression<bool>, IWithStringParameter
{
    public HasCharacterExpression(string characterID)
    {
        CharacterID = characterID;
    }

    public HasCharacterExpression() { }

    public string CharacterID { get; set; }
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This condition passes if the filterable has a character.";

    string IWithStringParameter.Parameter
    {
        get => CharacterID;
        set => CharacterID = value;
    }

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.CharacterIDs.Contains(CharacterID);
    }

    protected bool Equals(HasCharacterExpression other)
    {
        return base.Equals(other) && CharacterID == other.CharacterID;
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

        return Equals((HasCharacterExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), CharacterID);
    }

    public static bool operator ==(HasCharacterExpression left, HasCharacterExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasCharacterExpression left, HasCharacterExpression right)
    {
        return !Equals(left, right);
    }
}
