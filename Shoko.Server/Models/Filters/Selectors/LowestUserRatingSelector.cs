using System;
using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Selectors;

public class LowestUserRatingSelector : INumberSelector
{
    public bool UserDependent => true;
    public Func<IFilterable, double> Selector => f => Convert.ToDouble(f.LowestUserRating);
}
