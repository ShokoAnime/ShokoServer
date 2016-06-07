using System.IO;
using JMMContracts;
using JMMServer.Repositories;
using NLog;

namespace JMMServer
{
    public class JMMServiceImplementationImage : IJMMServerImage
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public byte[] GetImage(string entityID, int entityType, bool thumnbnailOnly)
        {
            var repAnime = new AniDB_AnimeRepository();
            var repPosters = new TvDB_ImagePosterRepository();
            var repEpisodes = new TvDB_EpisodeRepository();
            var repFanart = new TvDB_ImageFanartRepository();
            var repWideBanners = new TvDB_ImageWideBannerRepository();

            var repMoviePosters = new MovieDB_PosterRepository();
            var repMovieFanart = new MovieDB_FanartRepository();

            var repTraktFanart = new Trakt_ImageFanartRepository();
            var repTraktPosters = new Trakt_ImagePosterRepository();
            var repTraktEpisodes = new Trakt_EpisodeRepository();
            var repTraktFriends = new Trakt_FriendRepository();

            var imageType = (JMMImageType)entityType;

            switch (imageType)
            {
                case JMMImageType.AniDB_Cover:

                    var anime = repAnime.GetByAnimeID(int.Parse(entityID));
                    if (anime == null) return null;

                    if (File.Exists(anime.PosterPath))
                        return File.ReadAllBytes(anime.PosterPath);
                    logger.Trace("Could not find AniDB_Cover image: {0}", anime.PosterPath);
                    return null;

                case JMMImageType.AniDB_Character:

                    var repChar = new AniDB_CharacterRepository();
                    var chr = repChar.GetByID(int.Parse(entityID));
                    if (chr == null) return null;

                    if (File.Exists(chr.PosterPath))
                        return File.ReadAllBytes(chr.PosterPath);
                    logger.Trace("Could not find AniDB_Character image: {0}", chr.PosterPath);
                    return null;

                case JMMImageType.AniDB_Creator:

                    var repCreator = new AniDB_SeiyuuRepository();
                    var creator = repCreator.GetByID(int.Parse(entityID));
                    if (creator == null) return null;

                    if (File.Exists(creator.PosterPath))
                        return File.ReadAllBytes(creator.PosterPath);
                    logger.Trace("Could not find AniDB_Creator image: {0}", creator.PosterPath);
                    return null;

                case JMMImageType.TvDB_Cover:

                    var poster = repPosters.GetByID(int.Parse(entityID));
                    if (poster == null) return null;

                    if (File.Exists(poster.FullImagePath))
                        return File.ReadAllBytes(poster.FullImagePath);
                    logger.Trace("Could not find TvDB_Cover image: {0}", poster.FullImagePath);
                    return null;

                case JMMImageType.TvDB_Banner:

                    var wideBanner = repWideBanners.GetByID(int.Parse(entityID));
                    if (wideBanner == null) return null;

                    if (File.Exists(wideBanner.FullImagePath))
                        return File.ReadAllBytes(wideBanner.FullImagePath);
                    logger.Trace("Could not find TvDB_Banner image: {0}", wideBanner.FullImagePath);
                    return null;

                case JMMImageType.TvDB_Episode:

                    var ep = repEpisodes.GetByID(int.Parse(entityID));
                    if (ep == null) return null;

                    if (File.Exists(ep.FullImagePath))
                        return File.ReadAllBytes(ep.FullImagePath);
                    logger.Trace("Could not find TvDB_Episode image: {0}", ep.FullImagePath);
                    return null;

                case JMMImageType.TvDB_FanArt:

                    var fanart = repFanart.GetByID(int.Parse(entityID));
                    if (fanart == null) return null;

                    if (thumnbnailOnly)
                    {
                        if (File.Exists(fanart.FullThumbnailPath))
                            return File.ReadAllBytes(fanart.FullThumbnailPath);
                        logger.Trace("Could not find TvDB_FanArt image: {0}", fanart.FullThumbnailPath);
                        return null;
                    }
                    if (File.Exists(fanart.FullImagePath))
                        return File.ReadAllBytes(fanart.FullImagePath);
                    logger.Trace("Could not find TvDB_FanArt image: {0}", fanart.FullImagePath);
                    return null;

                case JMMImageType.MovieDB_Poster:

                    var mPoster = repMoviePosters.GetByID(int.Parse(entityID));
                    if (mPoster == null) return null;

                    // now find only the original size
                    mPoster = repMoviePosters.GetByOnlineID(mPoster.URL);
                    if (mPoster == null) return null;

                    if (File.Exists(mPoster.FullImagePath))
                        return File.ReadAllBytes(mPoster.FullImagePath);
                    logger.Trace("Could not find MovieDB_Poster image: {0}", mPoster.FullImagePath);
                    return null;

                case JMMImageType.MovieDB_FanArt:

                    var mFanart = repMovieFanart.GetByID(int.Parse(entityID));
                    if (mFanart == null) return null;

                    mFanart = repMovieFanart.GetByOnlineID(mFanart.URL);
                    if (mFanart == null) return null;

                    if (File.Exists(mFanart.FullImagePath))
                        return File.ReadAllBytes(mFanart.FullImagePath);
                    logger.Trace("Could not find MovieDB_FanArt image: {0}", mFanart.FullImagePath);
                    return null;

                case JMMImageType.Trakt_Fanart:

                    var tFanart = repTraktFanart.GetByID(int.Parse(entityID));
                    if (tFanart == null) return null;

                    if (File.Exists(tFanart.FullImagePath))
                        return File.ReadAllBytes(tFanart.FullImagePath);
                    logger.Trace("Could not find Trakt_Fanart image: {0}", tFanart.FullImagePath);
                    return null;

                case JMMImageType.Trakt_Poster:

                    var tPoster = repTraktPosters.GetByID(int.Parse(entityID));
                    if (tPoster == null) return null;

                    if (File.Exists(tPoster.FullImagePath))
                        return File.ReadAllBytes(tPoster.FullImagePath);
                    logger.Trace("Could not find Trakt_Poster image: {0}", tPoster.FullImagePath);
                    return null;

                case JMMImageType.Trakt_Episode:
                case JMMImageType.Trakt_WatchedEpisode:

                    var tEpisode = repTraktEpisodes.GetByID(int.Parse(entityID));
                    if (tEpisode == null) return null;

                    if (File.Exists(tEpisode.FullImagePath))
                        return File.ReadAllBytes(tEpisode.FullImagePath);
                    logger.Trace("Could not find Trakt_Episode image: {0}", tEpisode.FullImagePath);
                    return null;

                default:

                    return null;
            }
        }

        public byte[] GetImageUsingPath(string serverImagePath)
        {
            if (File.Exists(serverImagePath))
                return File.ReadAllBytes(serverImagePath);
            logger.Trace("Could not find AniDB_Cover image: {0}", serverImagePath);
            return null;
        }
    }
}