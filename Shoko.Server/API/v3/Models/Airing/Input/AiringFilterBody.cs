using FilterBody = Shoko.Server.API.v3.Models.Shoko.Filter.Input.CreateOrUpdateFilterBody;

#nullable enable
namespace Shoko.Server.API.v3.Models.Airing.Input;

/// <summary>
/// A filter to narrow an airing calendar read by.
/// </summary>
public class AiringFilterBody
{
    /// <summary>
    /// Only the anime of the Shoko series this filter passes, or <c>null</c>
    /// for no filter. Taken as <c>POST /api/v3/Filter/Preview/Series</c>
    /// takes it.
    /// </summary>
    public FilterBody? Filter { get; set; }
}
