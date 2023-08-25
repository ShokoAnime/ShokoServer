using System;
using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Selectors;

public class HighestUserRatingSelector : INumberSelector
{
    public bool UserDependent => true;
    public Func<IFilterable, double> Selector => f => Convert.ToDouble(f.HighestUserRating);
}
