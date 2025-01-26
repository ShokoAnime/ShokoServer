using System;
using Shoko.Server.Filters.Interfaces;
using Shoko.Server.Server;

namespace Shoko.Server.Filters.Info;

public class HasCreatorWithRoleExpression : FilterExpression<bool>, IWithStringParameter, IWithSecondStringParameter
{
    public HasCreatorWithRoleExpression(string creatorID, CreatorRoleType role)
    {
        CreatorID = creatorID;
        Role = role;
    }

    public HasCreatorWithRoleExpression() { }

    public string CreatorID { get; set; }
    public CreatorRoleType Role { get; set; }
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This condition passes if the filterable has a creator with the specified role.";

    string IWithStringParameter.Parameter
    {
        get => CreatorID;
        set => CreatorID = value;
    }

    string IWithSecondStringParameter.SecondParameter
    {
        get => Role.ToString();
        set => Role = Enum.Parse<CreatorRoleType>(value);
    }

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.CreatorRoles.TryGetValue(Role, out var roles) && roles.Contains(CreatorID);
    }

    protected bool Equals(HasCreatorWithRoleExpression other)
    {
        return base.Equals(other) && CreatorID == other.CreatorID && Role == other.Role;
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

        return Equals((HasCreatorWithRoleExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), CreatorID, (int)Role);
    }

    public static bool operator ==(HasCreatorWithRoleExpression left, HasCreatorWithRoleExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasCreatorWithRoleExpression left, HasCreatorWithRoleExpression right)
    {
        return !Equals(left, right);
    }
}
