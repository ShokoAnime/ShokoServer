using System;
using Shoko.Abstractions.Filtering;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Info;

public class HasCreatorExpression : FilterExpression<bool>, IWithStringParameter
{
    public HasCreatorExpression(string creatorID)
    {
        CreatorID = creatorID;
    }

    public HasCreatorExpression() { }

    public string CreatorID { get; set; }

    public override string HelpDescription => "This condition passes if the filterable has a creator.";

    string IWithStringParameter.Parameter
    {
        get => CreatorID;
        set => CreatorID = value;
    }

    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.CreatorIDs.Contains(CreatorID);
    }

    protected bool Equals(HasCreatorExpression other)
    {
        return base.Equals(other) && CreatorID == other.CreatorID;
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

        return Equals((HasCreatorExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), CreatorID);
    }

    public static bool operator ==(HasCreatorExpression left, HasCreatorExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasCreatorExpression left, HasCreatorExpression right)
    {
        return !Equals(left, right);
    }
}
