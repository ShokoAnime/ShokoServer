namespace Shoko.Server.Filters.Interfaces;

public interface IWithSecondStringSelectorParameter
{
    FilterExpression<string> Right { get; set; }
}
