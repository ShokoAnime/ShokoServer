using System;
using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters;

public abstract class SortingExpression : FilterExpression, ISortingExpression
{
    public bool Descending { get; set; } // take advantage of default(bool) being false
}

public abstract class SortingExpression<T> : SortingExpression, ISortingExpression<T> where T : IComparable
{
    public abstract T Evaluate(IFilterable f);
}
