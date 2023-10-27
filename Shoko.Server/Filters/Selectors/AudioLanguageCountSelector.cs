using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Selectors;

public class AudioLanguageCountSelector : FilterExpression<double>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This returns how many distinct audio languages are present in all of the files in a filterable";

    public override double Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.AudioLanguages.Count;
    }

    protected bool Equals(AudioLanguageCountSelector other)
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

        return Equals((AudioLanguageCountSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(AudioLanguageCountSelector left, AudioLanguageCountSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(AudioLanguageCountSelector left, AudioLanguageCountSelector right)
    {
        return !Equals(left, right);
    }
}
