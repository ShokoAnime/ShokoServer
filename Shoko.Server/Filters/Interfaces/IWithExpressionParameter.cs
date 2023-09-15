namespace Shoko.Server.Filters.Interfaces;

public interface IWithExpressionParameter
{
    FilterExpression<bool> Left { get; set; }
}
