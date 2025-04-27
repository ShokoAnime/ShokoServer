using System;
using System.Linq;
using Shoko.Server.Filters.Interfaces;
using Shoko.Server.Repositories;

namespace Shoko.Server.Filters.Info;

public class HasTagByIDExpression : FilterExpression<bool>, IWithStringParameter
{
    public HasTagByIDExpression(string parameter)
    {
        Parameter = parameter;
    }
    public HasTagByIDExpression() { }

    public string Parameter { get; set; }
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This condition passes if any of the anime have the specified AniDB tag by ID";
    public override string[] HelpPossibleParameters => RepoFactory.AniDB_Tag.GetAllForLocalSeries().Select(a => a.TagName.Replace('`', '\'')).ToArray();

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.AnidbTagIDs.Contains(Parameter);
    }

    protected bool Equals(HasTagByIDExpression other)
    {
        return base.Equals(other) && Parameter == other.Parameter;
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

        return Equals((HasTagByIDExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Parameter);
    }

    public static bool operator ==(HasTagByIDExpression left, HasTagByIDExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasTagByIDExpression left, HasTagByIDExpression right)
    {
        return !Equals(left, right);
    }
}
