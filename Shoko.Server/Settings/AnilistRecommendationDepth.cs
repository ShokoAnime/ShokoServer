namespace Shoko.Server.Settings;

/// <summary>
/// How far down AniList's recommendations to read for an anime. They come back
/// best first, so the tail is weakly rated entries pointing at anime few would
/// want shown, and each extra page is another request against the rate limit.
/// </summary>
public enum AnilistRecommendationDepth
{
    /// <summary>
    /// Only the first page, which is the 50 best rated. Cheapest: it rides
    /// along on the anime's own request and never costs a request of its own.
    /// </summary>
    FirstPage = 0,

    /// <summary>
    /// Keep reading while a page still ends on a well rated entry, and stop
    /// once it does not. Spends extra requests only on anime that have plenty
    /// of strong recommendations.
    /// </summary>
    WhileWellRated = 1,

    /// <summary>
    /// Every page, however weakly rated the tail is. The most complete and the
    /// most expensive, especially on a large collection.
    /// </summary>
    Everything = 2,
}
