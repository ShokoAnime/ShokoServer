using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Selectors.StringSelectors;

public class NamesSelector : FilterExpression<string>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This returns a bar separated list of all the names in a filterable. This includes series and group names. It is bar separated because names do sometimes contain commas.";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override string Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return string.Join("|", filterable.Names);
    }

    protected bool Equals(NamesSelector other)
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

        return Equals((NamesSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(NamesSelector left, NamesSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(NamesSelector left, NamesSelector right)
    {
        return !Equals(left, right);
    }
}
