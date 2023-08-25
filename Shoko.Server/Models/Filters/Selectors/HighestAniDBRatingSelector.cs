using System;
using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Selectors;

public class HighestAniDBRatingSelector : INumberSelector
{
    public bool UserDependent => false;
    public Func<IFilterable, double> Selector => f => Convert.ToDouble(f.HighestAniDBRating);
}
