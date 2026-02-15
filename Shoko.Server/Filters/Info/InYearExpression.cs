using System;
using System.Linq;
using Shoko.Abstractions.Filtering;
using Shoko.Server.Filters.Interfaces;
using Shoko.Server.Repositories;

namespace Shoko.Server.Filters.Info;

public class InYearExpression : FilterExpression<bool>, IWithNumberParameter
{
    public InYearExpression(int parameter)
    {
        Parameter = parameter;
    }
    public InYearExpression() { }

    public int Parameter { get; set; }

    public override bool TimeDependent => true;
    public override string HelpDescription => "This condition passes if any of the anime aired in the specified year";
    public override string[] HelpPossibleParameters => RepoFactory.AnimeSeries.GetAllYears().Select(a => a.ToString()).ToArray();

    double IWithNumberParameter.Parameter
    {
        get => Parameter;
        set => Parameter = (int)value;
    }

    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.Years.Contains(Parameter);
    }

    protected bool Equals(InYearExpression other)
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

        return Equals((InYearExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Parameter);
    }

    public static bool operator ==(InYearExpression left, InYearExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(InYearExpression left, InYearExpression right)
    {
        return !Equals(left, right);
    }
}
