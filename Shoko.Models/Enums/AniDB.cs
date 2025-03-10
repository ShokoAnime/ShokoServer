
namespace Shoko.Models.Enums;

public enum AniDBFileDeleteType
{
    Delete = 0,
    DeleteLocalOnly = 1,
    MarkDeleted = 2,
    MarkExternalStorage = 3,
    MarkUnknown = 4,
    MarkDisk = 5
}

public enum AniDBFile_State
{
    Unknown,
    HDD,
    Disk,
    Deleted,
    Remote
}

public enum AniDBVoteType
{
    Anime = 1,
    AnimeTemp = 2,
    Group = 3,
    Episode = 4
}

public enum AniDB_ResourceLinkType
{
    ANN = 1,
    MAL = 2, // MAL ID, there may be more than one
    AnimeNFO = 3, // Dead site.
    Site_JP = 4, // Official Japanese Site
    Site_EN = 5, // Official English Site
    Wiki_EN = 6, // wikipedia.com
    Wiki_JP = 7, // wikipedia.jp
    Syoboi = 8, // Airing Schedule (Japanese site)
    ALLCinema = 9,
    Anison = 10,
    DotLain = 11, // .lain (JP VA and anime site)
    VNDB = 14, // The Visual Novel Database, for related VN game, if any.
    Crunchyroll = 28, // Series page, not episodes
    Amazon = 32, // amazon.com
    Funimation = 34, // See Crunchyroll comment ☝
    Bangumi = 38, // Japanese site
    HiDive = 42, // Streaming service, series page.
}

public enum AnimeType
{
    Unknown = -1, // Not on AniDB or not yet assigned a type.
    Movie = 0,
    OVA = 1,
    TVSeries = 2,
    TVSpecial = 3,
    Web = 4,
    Other = 5,
    MusicVideo = 6,
}

public enum EpisodeType
{
    Episode = 1,
    Credits = 2,
    Special = 3,
    Trailer = 4,
    Parody = 5,
    Other = 6
}
