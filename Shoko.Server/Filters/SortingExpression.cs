using Shoko.Abstractions.Filtering;

#nullable enable
namespace Shoko.Server.Filters;

public abstract class SortingExpression : FilterExpression<object>, ISortingExpression
{
    public bool Descending { get; set; } // take advantage of default(bool) being false

    public SortingExpression? Next { get; set; }

    #region ISortingExpression Implementation

    ISortingExpression? ISortingExpression.Next => Next;

    #endregion
}
