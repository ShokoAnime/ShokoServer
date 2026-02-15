using System;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Filtering;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Info;

public class HasCreatorWithRoleExpression : FilterExpression<bool>, IWithStringParameter, IWithSecondStringParameter
{
    public HasCreatorWithRoleExpression(string creatorID, CrewRoleType role)
    {
        CreatorID = creatorID;
        Role = role;
    }

    public HasCreatorWithRoleExpression() { }

    public string CreatorID { get; set; }
    public CrewRoleType Role { get; set; }
    public override string HelpDescription => "This condition passes if the filterable has a creator with the specified role.";

    string IWithStringParameter.Parameter
    {
        get => CreatorID;
        set => CreatorID = value;
    }

    string IWithSecondStringParameter.SecondParameter
    {
        get => Role.ToString();
        set => Role = Enum.Parse<CrewRoleType>(value, ignoreCase: true);
    }

    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
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
