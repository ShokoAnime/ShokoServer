using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions;

/// <summary>
/// Fired on series info updates, currently, AniDB, TvDB, etc will trigger this
/// </summary>
public class SeriesInfoUpdatedEventArgs
{
    /// <summary>
    /// Where the data was updated. If there was a batch operation, this may just say the first one.
    /// </summary>
    public DataSourceEnum Type { get; set; }
    /// <summary>
    /// Anime info. This is the full data. A diff was not performed for this
    /// </summary>
    public IAnime AnimeInfo { get; set; }
}
