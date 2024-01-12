using System;
using Shoko.Server.Filters.Interfaces;
using Shoko.Server.Providers.AniDB;

namespace Shoko.Server.Filters.Files;

public class HasVideoSourceExpression : FilterExpression<bool>, IWithStringParameter
{
    public HasVideoSourceExpression(string parameter)
    {
        Parameter = parameter;
    }
    public HasVideoSourceExpression() { }

    public string Parameter { get; set; }
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This condition passes if any of the files have the specified video source";
    public override string[] HelpPossibleParameters => new[]
    {
        GetFile_Source.BluRay.ToString(), GetFile_Source.DVD.ToString(),
        GetFile_Source.Web.ToString(), GetFile_Source.TV.ToString(),
        GetFile_Source.HDTV.ToString(), GetFile_Source.Unknown.ToString(),
        GetFile_Source.Camcorder.ToString(), GetFile_Source.DTV.ToString(),
        GetFile_Source.VCD.ToString(), GetFile_Source.VHS.ToString(),
        GetFile_Source.SVCD.ToString(), GetFile_Source.HDDVD.ToString(),
        GetFile_Source.HKDVD.ToString(), GetFile_Source.LaserDisc.ToString()
    };

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.VideoSources.Contains(Parameter);
    }

    protected bool Equals(HasVideoSourceExpression other)
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

        return Equals((HasVideoSourceExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Parameter);
    }

    public static bool operator ==(HasVideoSourceExpression left, HasVideoSourceExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasVideoSourceExpression left, HasVideoSourceExpression right)
    {
        return !Equals(left, right);
    }
}
