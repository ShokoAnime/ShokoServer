using System;
using Shoko.Server.Filters.Interfaces;
using Shoko.Server.Providers.AniDB;

namespace Shoko.Server.Filters.Files;

public class HasSharedVideoSourceExpression : FilterExpression<bool>, IWithStringParameter
{
    public HasSharedVideoSourceExpression(string parameter)
    {
        Parameter = parameter;
    }
    public HasSharedVideoSourceExpression() { }

    public string Parameter { get; set; }
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This passes if all of the files have the video source provided in the parameter";
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

    public override bool Evaluate(IFilterable filterable)
    {
        return filterable.SharedVideoSources.Contains(Parameter);
    }

    protected bool Equals(HasSharedVideoSourceExpression other)
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

        return Equals((HasSharedVideoSourceExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Parameter);
    }

    public static bool operator ==(HasSharedVideoSourceExpression left, HasSharedVideoSourceExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasSharedVideoSourceExpression left, HasSharedVideoSourceExpression right)
    {
        return !Equals(left, right);
    }
}
