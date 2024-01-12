using System;
using Shoko.Server.Filters.Interfaces;
using Shoko.Server.Models;

namespace Shoko.Server.Filters.Files;

public class HasSubtitleLanguageExpression : FilterExpression<bool>, IWithStringParameter
{
    public HasSubtitleLanguageExpression(string parameter)
    {
        Parameter = parameter;
    }
    public HasSubtitleLanguageExpression() { }

    public string Parameter { get; set; }
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This condition passes if any of the files have the specified subtitle language";
    public override string[] HelpPossibleParameters => SVR_AniDB_File.GetPossibleSubtitleLanguages();

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.SubtitleLanguages.Contains(Parameter);
    }

    protected bool Equals(HasSubtitleLanguageExpression other)
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

        return Equals((HasSubtitleLanguageExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Parameter);
    }

    public static bool operator ==(HasSubtitleLanguageExpression left, HasSubtitleLanguageExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasSubtitleLanguageExpression left, HasSubtitleLanguageExpression right)
    {
        return !Equals(left, right);
    }
}
