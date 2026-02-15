using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.Selectors.NumberSelectors;

public class AutomaticTmdbEpisodeLinksSelector : FilterExpression<double>
{

    public override string HelpDescription => "This returns the number of automatic TMDB episode links for a series";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override double Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.AutomaticTmdbEpisodeLinks;
    }

    protected bool Equals(AutomaticTmdbEpisodeLinksSelector other)
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

        return Equals((AutomaticTmdbEpisodeLinksSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(AutomaticTmdbEpisodeLinksSelector left, AutomaticTmdbEpisodeLinksSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(AutomaticTmdbEpisodeLinksSelector left, AutomaticTmdbEpisodeLinksSelector right)
    {
        return !Equals(left, right);
    }
}
