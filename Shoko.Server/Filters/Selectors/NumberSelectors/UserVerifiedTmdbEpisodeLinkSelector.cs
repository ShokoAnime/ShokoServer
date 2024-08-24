using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Selectors.NumberSelectors;

public class UserVerifiedTmdbEpisodeLinkSelector : FilterExpression<double>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This returns the number of user verified TMDB episode links for a series";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override double Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.AutomaticTmdbEpisodeLinks;
    }

    protected bool Equals(UserVerifiedTmdbEpisodeLinkSelector other)
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

        return Equals((UserVerifiedTmdbEpisodeLinkSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(UserVerifiedTmdbEpisodeLinkSelector left, UserVerifiedTmdbEpisodeLinkSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(UserVerifiedTmdbEpisodeLinkSelector left, UserVerifiedTmdbEpisodeLinkSelector right)
    {
        return !Equals(left, right);
    }
}
