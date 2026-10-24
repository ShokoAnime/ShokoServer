namespace Shoko.Abstractions.User.Models;

/// <summary>
///   What the next-up search of a series looks at.
/// </summary>
public sealed record NextUpOptions
{
    /// <summary>
    ///   Whether the episode the user left part-way through comes first. When
    ///   <c>false</c>, a series with one gives no next-up episode at all.
    /// </summary>
    public bool IncludeCurrentlyWatching { get; init; }

    /// <summary>
    ///   Whether episodes that aired but have no video count.
    /// </summary>
    public bool IncludeMissing { get; init; }

    /// <summary>
    ///   Whether episodes that have not aired yet count.
    /// </summary>
    public bool IncludeUnaired { get; init; }

    /// <summary>
    ///   Whether to continue after the episode last watched, watched or not,
    ///   when the user is watching the series again.
    /// </summary>
    public bool IncludeRewatching { get; init; }

    /// <summary>
    ///   Whether specials count.
    /// </summary>
    public bool IncludeSpecials { get; init; } = true;

    /// <summary>
    ///   Whether other episodes count. They come after the regular episodes
    ///   and before the specials.
    /// </summary>
    public bool IncludeOthers { get; init; }

    /// <summary>
    ///   Whether to give nothing when the next episode is the first regular
    ///   one, leaving out series the user has not started, as the dashboard
    ///   does.
    /// </summary>
    public bool ExcludeFirstEpisode { get; init; }
}
