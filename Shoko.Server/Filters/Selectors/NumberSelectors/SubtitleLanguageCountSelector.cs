using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Selectors.NumberSelectors;

public class SubtitleLanguageCountSelector : FilterExpression<double>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This returns how many distinct subtitle languages are present in all of the files in a filterable";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override double Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.SubtitleLanguages.Count;
    }

    protected bool Equals(SubtitleLanguageCountSelector other)
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

        return Equals((SubtitleLanguageCountSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(SubtitleLanguageCountSelector left, SubtitleLanguageCountSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(SubtitleLanguageCountSelector left, SubtitleLanguageCountSelector right)
    {
        return !Equals(left, right);
    }
}
