using System.Runtime.Serialization;
using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters;

public class FilterExpression : IFilterExpression
{
    public int FilterExpressionID { get; set; }
    [IgnoreDataMember]
    public virtual bool TimeDependent => false;
    [IgnoreDataMember]
    public virtual bool UserDependent => false;
}

public abstract class FilterExpression<T> : FilterExpression, IFilterExpression<T>
{
    public abstract T Evaluate(IFilterable f);
}
