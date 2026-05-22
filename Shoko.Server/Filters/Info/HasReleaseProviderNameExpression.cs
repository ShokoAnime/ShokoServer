using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.Filters.Interfaces;

#pragma warning disable CS0618
namespace Shoko.Server.Filters.Info;

public class HasReleaseProviderNameExpression : FilterExpression<bool>, IWithStringParameter
{
    public HasReleaseProviderNameExpression(string parameter)
    {
        Parameter = parameter;
    }

    public HasReleaseProviderNameExpression() { }

    public string Parameter { get; set; }

    public override bool TimeDependent => false;

    public override bool UserDependent => false;

    public override string HelpDescription => "This condition passes if any of the anime have the files of specified release provider name";

    public override string[] HelpPossibleParameters => ISystemService.StaticServices.GetRequiredService<IVideoReleaseService>().GetStoredReleaseProviderNames().ToArray();

    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? now)
    {
        return filterable.ReleaseProviderNames.Contains(Parameter);
    }

    protected bool Equals(HasReleaseProviderNameExpression other)
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

        return Equals((HasReleaseProviderNameExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Parameter);
    }
    public static bool operator ==(HasReleaseProviderNameExpression left, HasReleaseProviderNameExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasReleaseProviderNameExpression left, HasReleaseProviderNameExpression right)
    {
        return !Equals(left, right);
    }
}
