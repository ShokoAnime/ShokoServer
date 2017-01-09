namespace Shoko.Models
{
    public enum ImageEntityType
    {
        AniDB_Cover = 1, // use AnimeID
        AniDB_Character = 2, // use CharID
        AniDB_Creator = 3, // use CreatorID
        TvDB_Banner = 4, // use TvDB Banner ID
        TvDB_Cover = 5, // use TvDB Cover ID
        TvDB_Episode = 6, // use TvDB Episode ID
        TvDB_FanArt = 7, // use TvDB FanArt ID
        MovieDB_FanArt = 8,
        MovieDB_Poster = 9,
        Trakt_Poster = 10,
        Trakt_Fanart = 11,
        Trakt_Episode = 12,
        Trakt_Friend = 13,
        Trakt_ActivityScrobble = 14,
        Trakt_CommentUser = 15,
        Trakt_WatchedEpisode = 16
    }
}