namespace Shoko.Server.Filters.Interfaces;

public interface IWithSecondExpressionParameter
{
    FilterExpression<bool> Right { get; set; }
}
