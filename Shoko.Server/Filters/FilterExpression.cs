using System.Runtime.Serialization;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters;

public class FilterExpression : IFilterExpression
{
    public int FilterExpressionID { get; set; }
    [IgnoreDataMember] public virtual bool TimeDependent => false;
    [IgnoreDataMember] public virtual bool UserDependent => false;
}

public abstract class FilterExpression<T> : FilterExpression, IFilterExpression<T>
{
    public abstract T Evaluate(Filterable f);
}
