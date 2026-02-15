using System;
using System.Collections.Generic;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.Selectors.StringSetSelectors;

public class AnidbTagsSelector : FilterExpression<IReadOnlySet<string>>
{

    public override string HelpDescription => "This returns a set of all the AniDB tags in a filterable.";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override IReadOnlySet<string> Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.AnidbTags;
    }

    protected bool Equals(AnidbTagsSelector other)
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

        return Equals((AnidbTagsSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(AnidbTagsSelector left, AnidbTagsSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(AnidbTagsSelector left, AnidbTagsSelector right)
    {
        return !Equals(left, right);
    }
}
