namespace Shoko.Server.API.v3.Models.TMDB;

/// <summary>
///   What one of TMDB's alternate orderings of a show follows, as the APIv3
///   TMDB models name it.
/// </summary>
public enum AlternateOrderingType
{
    Unknown = 0,
    OriginalAirDate = 1,
    Absolute = 2,
    DVD = 3,
    Digital = 4,
    StoryArc = 5,
    Production = 6,
    TV = 7
}
