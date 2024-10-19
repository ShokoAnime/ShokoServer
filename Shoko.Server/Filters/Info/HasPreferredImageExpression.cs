using System;
using System.Linq;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Filters.Interfaces;
using Shoko.Server.Repositories;

namespace Shoko.Server.Filters.Info;

public class HasPreferredImageExpression : FilterExpression<bool>, IWithStringParameter
{
    public HasPreferredImageExpression(string parameter)
    {
        if (Enum.TryParse<ImageEntityType>(parameter, out var imageEntityType))
            imageEntityType = ImageEntityType.None;
        Parameter = imageEntityType;
    }

    public HasPreferredImageExpression() { }

    public ImageEntityType Parameter { get; set; }
    public override bool TimeDependent => true;
    public override bool UserDependent => false;
    public override string HelpDescription => "This condition passes if any of the anime has the preferred image type.";
    public override string[] HelpPossibleParameters => RepoFactory.AnimeSeries.GetAllImageTypes().Select(a => a.ToString()).ToArray();

    string IWithStringParameter.Parameter
    {
        get => Parameter.ToString();
        set
        {
            if (Enum.TryParse<ImageEntityType>(value, out var imageEntityType))
                imageEntityType = ImageEntityType.None;
            Parameter = imageEntityType;
        }
    }

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.PreferredImageTypes.Contains(Parameter);
    }

    protected bool Equals(HasPreferredImageExpression other)
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

        return Equals((HasPreferredImageExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Parameter);
    }

    public static bool operator ==(HasPreferredImageExpression left, HasPreferredImageExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasPreferredImageExpression left, HasPreferredImageExpression right)
    {
        return !Equals(left, right);
    }
}
