namespace Shoko.Server.API.v3.Models.Shoko;

/// <summary>
/// How many groups and series a filter lists, counted as its group and series
/// listings would list them for the same request.
/// </summary>
public class FilterCount
{
    /// <summary>
    /// The number of groups the filter's group listing would list.
    /// </summary>
    public required int Groups { get; init; }

    /// <summary>
    /// The number of series the filter's series listing would list.
    /// </summary>
    public required int Series { get; init; }
}
