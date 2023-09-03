using System;
using System.Collections.Generic;
using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters;

public abstract class SortingExpression : FilterExpression, ISortingExpression
{
    public bool Descending { get; set; } // take advantage of default(bool) being false
}

public abstract class SortingExpression<T> : SortingExpression, ISortingExpression<T>, IComparer<IFilterable> where T : IComparable
{
    public SortingExpression<T> Next { get; set; }
    public abstract T Evaluate(IFilterable f);
    public virtual int Compare(IFilterable x, IFilterable y)
    {
        var valueX = Evaluate(x);
        var valueY = Evaluate(y);
        if (Equals(valueX, valueY)) return Next?.Compare(x, y) ?? 0;
        if (valueX == null) return 1;
        if (valueY == null) return -1;
        return valueX.CompareTo(valueY);
    }
}
