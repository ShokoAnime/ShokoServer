using System;
using Shoko.Server.Filters.Interfaces;
using Shoko.Server.Models;

namespace Shoko.Server.Filters.Files;

public class HasSharedSubtitleLanguageExpression : FilterExpression<bool>, IWithStringParameter
{
    public HasSharedSubtitleLanguageExpression(string parameter)
    {
        Parameter = parameter;
    }
    public HasSharedSubtitleLanguageExpression() { }

    public string Parameter { get; set; }
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This passes if all of the files have the subtitle language provided in the parameter";
    public override string[] HelpPossibleParameters => SVR_AniDB_File.GetPossibleSubtitleLanguages();

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.SharedSubtitleLanguages.Contains(Parameter);
    }

    protected bool Equals(HasSharedSubtitleLanguageExpression other)
    {
        return base.Equals(other) && string.Equals(Parameter, other.Parameter, StringComparison.InvariantCultureIgnoreCase);
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

        return Equals((HasSharedSubtitleLanguageExpression)obj);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(base.GetHashCode());
        hashCode.Add(Parameter, StringComparer.InvariantCultureIgnoreCase);
        return hashCode.ToHashCode();
    }

    public static bool operator ==(HasSharedSubtitleLanguageExpression left, HasSharedSubtitleLanguageExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasSharedSubtitleLanguageExpression left, HasSharedSubtitleLanguageExpression right)
    {
        return !Equals(left, right);
    }
}
