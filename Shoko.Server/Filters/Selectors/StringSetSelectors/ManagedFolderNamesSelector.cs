using System;
using System.Collections.Generic;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.Selectors.StringSetSelectors;

public class ManagedFolderNamesSelector : FilterExpression<IReadOnlySet<string>>
{

    public override string HelpDescription => "This returns a set of the managed folder names in a filterable";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override IReadOnlySet<string> Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.ManagedFolderNames;
    }

    protected bool Equals(ManagedFolderNamesSelector other)
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

        return Equals((ManagedFolderNamesSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(ManagedFolderNamesSelector left, ManagedFolderNamesSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(ManagedFolderNamesSelector left, ManagedFolderNamesSelector right)
    {
        return !Equals(left, right);
    }
}
