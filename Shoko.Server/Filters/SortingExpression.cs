using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters;

public abstract class SortingExpression : FilterExpression<object>, ISortingExpression
{
    public bool Descending { get; set; } // take advantage of default(bool) being false
    public SortingExpression Next { get; set; }
}
