namespace Shoko.Server.Filters.Interfaces;

public interface IWithNumberSelectorParameter
{
    FilterExpression<double> Left { get; set; }
}
