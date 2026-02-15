using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.Selectors.DateSelectors;

public class AddedDateSelector : FilterExpression<DateTime?>
{
    public override string HelpDescription => "This returns the date that a filterable was created";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override DateTime? Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.AddedDate;
    }

    protected bool Equals(AddedDateSelector other)
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

        return Equals((AddedDateSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(AddedDateSelector left, AddedDateSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(AddedDateSelector left, AddedDateSelector right)
    {
        return !Equals(left, right);
    }
}
