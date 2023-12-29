using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Selectors.StringSelectors;

public class FilePathSelector : FilterExpression<string>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This returns a comma separated list of the file paths in a filterable";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override string Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return string.Join(",", filterable.FilePaths);
    }

    protected bool Equals(FilePathSelector other)
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

        return Equals((FilePathSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(FilePathSelector left, FilePathSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(FilePathSelector left, FilePathSelector right)
    {
        return !Equals(left, right);
    }
}
