using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Info;

public class HasVideoFilesExpression : FilterExpression<bool>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This condition passes if any of the anime have any video files locally.";

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.VideoFiles > 0;
    }

    protected bool Equals(HasVideoFilesExpression other)
    {
        return base.Equals(other);
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

        return Equals((HasVideoFilesExpression)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(HasVideoFilesExpression left, HasVideoFilesExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasVideoFilesExpression left, HasVideoFilesExpression right)
    {
        return !Equals(left, right);
    }
}
