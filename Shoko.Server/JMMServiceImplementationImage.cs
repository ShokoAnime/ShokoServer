using System.IO;
using Shoko.Models;
using Shoko.Models.Server;
using Shoko.Server.Repositories.Direct;
using NLog;
using Nancy;
using Nancy.Rest.Module;
using Shoko.Models.Interfaces;
using Shoko.Server.Entities;
using Shoko.Server.Repositories;

namespace Shoko.Server
{
    public class JMMServiceImplementationImage : IJMMServerImage
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public Stream GetImage(string entityID, int entityType, bool thumnbnailOnly)
        {
            JMMImageType imageType = (JMMImageType) entityType;
	

            switch (imageType)
            {
                case JMMImageType.AniDB_Cover:

                    SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(int.Parse(entityID));
                    if (anime == null) return null;

					if (File.Exists(anime.PosterPath))
					{
                        return new StreamWithContentType(File.OpenRead(anime.PosterPath), MimeTypes.GetMimeType(anime.PosterPath));
					}
					else
					{
						logger.Trace("Could not find AniDB_Cover image: {0}", anime.PosterPath);
						return null;
					}

                case JMMImageType.AniDB_Character:
                    SVR_AniDB_Character chr = RepoFactory.AniDB_Character.GetByID(int.Parse(entityID));
                    if (chr == null) return null;

                    if (File.Exists(chr.PosterPath))
					{
                        return new StreamWithContentType(File.OpenRead(chr.PosterPath), MimeTypes.GetMimeType(chr.PosterPath));
					}
                    else
                    {
                        logger.Trace("Could not find AniDB_Character image: {0}", chr.PosterPath);
                        return null;
                    }

                case JMMImageType.AniDB_Creator:
                    SVR_AniDB_Seiyuu creator = RepoFactory.AniDB_Seiyuu.GetByID(int.Parse(entityID));
                    if (creator == null) return null;

					if (File.Exists(creator.PosterPath))
					{
                        return new StreamWithContentType(File.OpenRead(creator.PosterPath), MimeTypes.GetMimeType(creator.PosterPath));
					}
					else
					{
						logger.Trace("Could not find AniDB_Creator image: {0}", creator.PosterPath);
						return null;
					}

                case JMMImageType.TvDB_Cover:

                    TvDB_ImagePoster poster = RepoFactory.TvDB_ImagePoster.GetByID(int.Parse(entityID));
                    if (poster == null) return null;

					if (File.Exists(poster.GetFullImagePath()))
					{
                        return new StreamWithContentType(File.OpenRead(poster.GetFullImagePath()), MimeTypes.GetMimeType(poster.GetFullImagePath()));
					}
					else
					{
						logger.Trace("Could not find TvDB_Cover image: {0}", poster.GetFullImagePath());
						return null;
					}

                case JMMImageType.TvDB_Banner:

                    TvDB_ImageWideBanner wideBanner = RepoFactory.TvDB_ImageWideBanner.GetByID(int.Parse(entityID));
                    if (wideBanner == null) return null;

					if (File.Exists(wideBanner.GetFullImagePath()))
					{
                        return new StreamWithContentType(File.OpenRead(wideBanner.GetFullImagePath()), MimeTypes.GetMimeType(wideBanner.GetFullImagePath()));
                    }
                    else
					{
						logger.Trace("Could not find TvDB_Banner image: {0}", wideBanner.GetFullImagePath());
						return null;
					}

                case JMMImageType.TvDB_Episode:

                    TvDB_Episode ep = RepoFactory.TvDB_Episode.GetByID(int.Parse(entityID));
                    if (ep == null) return null;

					if (File.Exists(ep.GetFullImagePath()))
					{
                        return new StreamWithContentType(File.OpenRead(ep.GetFullImagePath()), MimeTypes.GetMimeType(ep.GetFullImagePath()));
					}
					else
					{
						logger.Trace("Could not find TvDB_Episode image: {0}", ep.GetFullImagePath());
						return null;
					}

                case JMMImageType.TvDB_FanArt:

                    TvDB_ImageFanart fanart = RepoFactory.TvDB_ImageFanart.GetByID(int.Parse(entityID));
                    if (fanart == null) return null;

                    if (thumnbnailOnly)
                    {
						if (File.Exists(fanart.GetFullThumbnailPath()))
						{
                            return new StreamWithContentType(File.OpenRead(fanart.GetFullThumbnailPath()), MimeTypes.GetMimeType(fanart.GetFullThumbnailPath()));
						}
						else
						{
							logger.Trace("Could not find TvDB_FanArt image: {0}", fanart.GetFullThumbnailPath());
							return null;
						}
                    }
                    else
                    {
						if (File.Exists(fanart.GetFullImagePath()))
						{
                            return new StreamWithContentType(File.OpenRead(fanart.GetFullImagePath()), MimeTypes.GetMimeType(fanart.GetFullImagePath()));
						}
						else
						{
							logger.Trace("Could not find TvDB_FanArt image: {0}", fanart.GetFullImagePath());
							return null;
						}
                    }

                case JMMImageType.MovieDB_Poster:

                    MovieDB_Poster mPoster = RepoFactory.MovieDB_Poster.GetByID(int.Parse(entityID));
                    if (mPoster == null) return null;

                    // now find only the original size
                    mPoster = RepoFactory.MovieDB_Poster.GetByOnlineID(mPoster.URL);
                    if (mPoster == null) return null;

					if (File.Exists(mPoster.GetFullImagePath()))
					{
                        return new StreamWithContentType(File.OpenRead(mPoster.GetFullImagePath()), MimeTypes.GetMimeType(mPoster.GetFullImagePath()));
					}
					else
					{
						logger.Trace("Could not find MovieDB_Poster image: {0}", mPoster.GetFullImagePath());
						return null;
					}

                case JMMImageType.MovieDB_FanArt:

                    MovieDB_Fanart mFanart = RepoFactory.MovieDB_Fanart.GetByID(int.Parse(entityID));
                    if (mFanart == null) return null;

                    mFanart = RepoFactory.MovieDB_Fanart.GetByOnlineID(mFanart.URL);
                    if (mFanart == null) return null;

					if (File.Exists(mFanart.GetFullImagePath()))
					{
                        return new StreamWithContentType(File.OpenRead(mFanart.GetFullImagePath()), MimeTypes.GetMimeType(mFanart.GetFullImagePath()));
					}
					else
					{
						logger.Trace("Could not find MovieDB_FanArt image: {0}", mFanart.GetFullImagePath());
						return null;
					}

                case JMMImageType.Trakt_Fanart:

                    Trakt_ImageFanart tFanart = RepoFactory.Trakt_ImageFanart.GetByID(int.Parse(entityID));
                    if (tFanart == null) return null;

					if (File.Exists(tFanart.GetFullImagePath()))
					{
                        return new StreamWithContentType(File.OpenRead(tFanart.GetFullImagePath()), MimeTypes.GetMimeType(tFanart.GetFullImagePath()));
					}
					else
					{
						logger.Trace("Could not find Trakt_Fanart image: {0}", tFanart.GetFullImagePath());
						return null;
					}

                case JMMImageType.Trakt_Poster:

                    Trakt_ImagePoster tPoster = RepoFactory.Trakt_ImagePoster.GetByID(int.Parse(entityID));
                    if (tPoster == null) return null;

					if (File.Exists(tPoster.GetFullImagePath()))
					{
                        return new StreamWithContentType(File.OpenRead(tPoster.GetFullImagePath()), MimeTypes.GetMimeType(tPoster.GetFullImagePath()));
					}
					else
					{
						logger.Trace("Could not find Trakt_Poster image: {0}", tPoster.GetFullImagePath());
						return null;
					}

                case JMMImageType.Trakt_Episode:
                case JMMImageType.Trakt_WatchedEpisode:

                    Trakt_Episode tEpisode = RepoFactory.Trakt_Episode.GetByID(int.Parse(entityID));
                    if (tEpisode == null) return null;

					if (File.Exists(tEpisode.GetFullImagePath()))
					{
                        return new StreamWithContentType(File.OpenRead(tEpisode.GetFullImagePath()), MimeTypes.GetMimeType(tEpisode.GetFullImagePath()));
					}
					else
					{
						logger.Trace("Could not find Trakt_Episode image: {0}", tEpisode.GetFullImagePath());
						return null;
					}

                default:

                    return null;
            }
        }

        public Stream GetImageUsingPath(string serverImagePath)
        {
			if (File.Exists(serverImagePath))
			{
			    return new StreamWithContentType(File.OpenRead(serverImagePath), MimeTypes.GetMimeType(serverImagePath));
			}
			else
			{
				logger.Trace("Could not find AniDB_Cover image: {0}", serverImagePath);
				return null;
			}
        }
    }
}