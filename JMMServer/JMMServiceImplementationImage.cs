using System.IO;
using JMMContracts;
using JMMServer.Entities;
using JMMServer.Repositories;
using JMMServer.Repositories.Direct;
using NLog;
using Nancy;

namespace JMMServer
{
    public class JMMServiceImplementationImage : IJMMServerImage
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

		public byte[] GetImage(string entityID, int entityType, bool thumnbnailOnly)
		{
			string str;
			return GetImage(entityID, entityType, thumnbnailOnly, out str);
		}

        public byte[] GetImage(string entityID, int entityType, bool thumnbnailOnly, out string contentType)
        {
            JMMImageType imageType = (JMMImageType) entityType;
			contentType = "";

            switch (imageType)
            {
                case JMMImageType.AniDB_Cover:

                    AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(int.Parse(entityID));
                    if (anime == null) return null;

					if (File.Exists(anime.PosterPath))
					{
						contentType = MimeTypes.GetMimeType(anime.PosterPath);
						return File.ReadAllBytes(anime.PosterPath);
					}
					else
					{
						logger.Trace("Could not find AniDB_Cover image: {0}", anime.PosterPath);
						return null;
					}

                case JMMImageType.AniDB_Character:
                    AniDB_Character chr = RepoFactory.AniDB_Character.GetByID(int.Parse(entityID));
                    if (chr == null) return null;

                    if (File.Exists(chr.PosterPath))
					{
						contentType = MimeTypes.GetMimeType(chr.PosterPath);
						return File.ReadAllBytes(chr.PosterPath);
					}
                    else
                    {
                        logger.Trace("Could not find AniDB_Character image: {0}", chr.PosterPath);
                        return null;
                    }

                case JMMImageType.AniDB_Creator:
                    AniDB_Seiyuu creator = RepoFactory.AniDB_Seiyuu.GetByID(int.Parse(entityID));
                    if (creator == null) return null;

					if (File.Exists(creator.PosterPath))
					{
						contentType = MimeTypes.GetMimeType(creator.PosterPath);
						return File.ReadAllBytes(creator.PosterPath);
					}
					else
					{
						logger.Trace("Could not find AniDB_Creator image: {0}", creator.PosterPath);
						return null;
					}

                case JMMImageType.TvDB_Cover:

                    TvDB_ImagePoster poster = RepoFactory.TvDB_ImagePoster.GetByID(int.Parse(entityID));
                    if (poster == null) return null;

					if (File.Exists(poster.FullImagePath))
					{
						contentType = MimeTypes.GetMimeType(poster.FullImagePath);
						return File.ReadAllBytes(poster.FullImagePath);
					}
					else
					{
						logger.Trace("Could not find TvDB_Cover image: {0}", poster.FullImagePath);
						return null;
					}

                case JMMImageType.TvDB_Banner:

                    TvDB_ImageWideBanner wideBanner = RepoFactory.TvDB_ImageWideBanner.GetByID(int.Parse(entityID));
                    if (wideBanner == null) return null;

					if (File.Exists(wideBanner.FullImagePath))
					{
						contentType = MimeTypes.GetMimeType(wideBanner.FullImagePath);
						return File.ReadAllBytes(wideBanner.FullImagePath);
					}
					else
					{
						logger.Trace("Could not find TvDB_Banner image: {0}", wideBanner.FullImagePath);
						return null;
					}

                case JMMImageType.TvDB_Episode:

                    TvDB_Episode ep = RepoFactory.TvDB_Episode.GetByID(int.Parse(entityID));
                    if (ep == null) return null;

					if (File.Exists(ep.FullImagePath))
					{
						contentType = MimeTypes.GetMimeType(ep.FullImagePath);
						return File.ReadAllBytes(ep.FullImagePath);
					}
					else
					{
						logger.Trace("Could not find TvDB_Episode image: {0}", ep.FullImagePath);
						return null;
					}

                case JMMImageType.TvDB_FanArt:

                    TvDB_ImageFanart fanart = RepoFactory.TvDB_ImageFanart.GetByID(int.Parse(entityID));
                    if (fanart == null) return null;

                    if (thumnbnailOnly)
                    {
						if (File.Exists(fanart.FullThumbnailPath))
						{
							contentType = MimeTypes.GetMimeType(fanart.FullThumbnailPath);
							return File.ReadAllBytes(fanart.FullThumbnailPath);
						}
						else
						{
							logger.Trace("Could not find TvDB_FanArt image: {0}", fanart.FullThumbnailPath);
							return null;
						}
                    }
                    else
                    {
						if (File.Exists(fanart.FullImagePath))
						{
							contentType = MimeTypes.GetMimeType(fanart.FullImagePath);
							return File.ReadAllBytes(fanart.FullImagePath);
						}
						else
						{
							logger.Trace("Could not find TvDB_FanArt image: {0}", fanart.FullImagePath);
							return null;
						}
                    }

                case JMMImageType.MovieDB_Poster:

                    MovieDB_Poster mPoster = RepoFactory.MovieDB_Poster.GetByID(int.Parse(entityID));
                    if (mPoster == null) return null;

                    // now find only the original size
                    mPoster = RepoFactory.MovieDB_Poster.GetByOnlineID(mPoster.URL);
                    if (mPoster == null) return null;

					if (File.Exists(mPoster.FullImagePath))
					{
						contentType = MimeTypes.GetMimeType(mPoster.FullImagePath);
						return File.ReadAllBytes(mPoster.FullImagePath);
					}
					else
					{
						logger.Trace("Could not find MovieDB_Poster image: {0}", mPoster.FullImagePath);
						return null;
					}

                case JMMImageType.MovieDB_FanArt:

                    MovieDB_Fanart mFanart = RepoFactory.MovieDB_Fanart.GetByID(int.Parse(entityID));
                    if (mFanart == null) return null;

                    mFanart = RepoFactory.MovieDB_Fanart.GetByOnlineID(mFanart.URL);
                    if (mFanart == null) return null;

					if (File.Exists(mFanart.FullImagePath))
					{
						contentType = MimeTypes.GetMimeType(mFanart.FullImagePath);
						return File.ReadAllBytes(mFanart.FullImagePath);
					}
					else
					{
						logger.Trace("Could not find MovieDB_FanArt image: {0}", mFanart.FullImagePath);
						return null;
					}

                case JMMImageType.Trakt_Fanart:

                    Trakt_ImageFanart tFanart = RepoFactory.Trakt_ImageFanart.GetByID(int.Parse(entityID));
                    if (tFanart == null) return null;

					if (File.Exists(tFanart.FullImagePath))
					{
						contentType = MimeTypes.GetMimeType(tFanart.FullImagePath);
						return File.ReadAllBytes(tFanart.FullImagePath);
					}
					else
					{
						logger.Trace("Could not find Trakt_Fanart image: {0}", tFanart.FullImagePath);
						return null;
					}

                case JMMImageType.Trakt_Poster:

                    Trakt_ImagePoster tPoster = RepoFactory.Trakt_ImagePoster.GetByID(int.Parse(entityID));
                    if (tPoster == null) return null;

					if (File.Exists(tPoster.FullImagePath))
					{
						contentType = MimeTypes.GetMimeType(tPoster.FullImagePath);
						return File.ReadAllBytes(tPoster.FullImagePath);
					}
					else
					{
						logger.Trace("Could not find Trakt_Poster image: {0}", tPoster.FullImagePath);
						return null;
					}

                case JMMImageType.Trakt_Episode:
                case JMMImageType.Trakt_WatchedEpisode:

                    Trakt_Episode tEpisode = RepoFactory.Trakt_Episode.GetByID(int.Parse(entityID));
                    if (tEpisode == null) return null;

					if (File.Exists(tEpisode.FullImagePath))
					{
						contentType = MimeTypes.GetMimeType(tEpisode.FullImagePath);
						return File.ReadAllBytes(tEpisode.FullImagePath);
					}
					else
					{
						logger.Trace("Could not find Trakt_Episode image: {0}", tEpisode.FullImagePath);
						return null;
					}

                default:

                    return null;
            }
        }

        public byte[] GetImageUsingPath(string serverImagePath)
        {
			if (File.Exists(serverImagePath))
			{
				return File.ReadAllBytes(serverImagePath);
			}
			else
			{
				logger.Trace("Could not find AniDB_Cover image: {0}", serverImagePath);
				return null;
			}
        }
    }
}