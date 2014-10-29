using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using JMMContracts;
using JMMServer.Repositories;
using JMMServer.Entities;
using NLog;
using JMMServer.Commands;

namespace JMMServer
{
	public class JMMServiceImplementationImage : IJMMServerImage
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		public byte[] GetImage(string entityID, int entityType, bool thumnbnailOnly)
		{
			AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
			TvDB_ImagePosterRepository repPosters = new TvDB_ImagePosterRepository();
			TvDB_EpisodeRepository repEpisodes = new TvDB_EpisodeRepository();
			TvDB_ImageFanartRepository repFanart = new TvDB_ImageFanartRepository();
			TvDB_ImageWideBannerRepository repWideBanners = new TvDB_ImageWideBannerRepository();

			MovieDB_PosterRepository repMoviePosters = new MovieDB_PosterRepository();
			MovieDB_FanartRepository repMovieFanart = new MovieDB_FanartRepository();

			Trakt_ImageFanartRepository repTraktFanart = new Trakt_ImageFanartRepository();
			Trakt_ImagePosterRepository repTraktPosters = new Trakt_ImagePosterRepository();
			Trakt_EpisodeRepository repTraktEpisodes = new Trakt_EpisodeRepository();
			Trakt_FriendRepository repTraktFriends = new Trakt_FriendRepository();

			JMMImageType imageType = (JMMImageType)entityType;

			switch (imageType)
			{
				case JMMImageType.AniDB_Cover:

					AniDB_Anime anime = repAnime.GetByAnimeID(int.Parse(entityID));
					if (anime == null) return null;

					if (File.Exists(anime.PosterPath))
						return File.ReadAllBytes(anime.PosterPath);
					else
					{
						logger.Trace("Could not find AniDB_Cover image: {0}", anime.PosterPath);
						return null;
					}

				case JMMImageType.AniDB_Character:

					AniDB_CharacterRepository repChar = new AniDB_CharacterRepository();
					AniDB_Character chr = repChar.GetByID(int.Parse(entityID));
					if (chr == null) return null;

					if (File.Exists(chr.PosterPath))
						return File.ReadAllBytes(chr.PosterPath);
					else
					{
						logger.Trace("Could not find AniDB_Character image: {0}", chr.PosterPath);
						return null;
					}

				case JMMImageType.AniDB_Creator:

					AniDB_SeiyuuRepository repCreator = new AniDB_SeiyuuRepository();
					AniDB_Seiyuu creator = repCreator.GetByID(int.Parse(entityID));
					if (creator == null) return null;

					if (File.Exists(creator.PosterPath))
						return File.ReadAllBytes(creator.PosterPath);
					else
					{
						logger.Trace("Could not find AniDB_Creator image: {0}", creator.PosterPath);
						return null;
					}

				case JMMImageType.TvDB_Cover:

					TvDB_ImagePoster poster = repPosters.GetByID(int.Parse(entityID));
					if (poster == null) return null;

					if (File.Exists(poster.FullImagePath))
						return File.ReadAllBytes(poster.FullImagePath);
					else
					{
						logger.Trace("Could not find TvDB_Cover image: {0}", poster.FullImagePath);
						return null;
					}

				case JMMImageType.TvDB_Banner:

					TvDB_ImageWideBanner wideBanner = repWideBanners.GetByID(int.Parse(entityID));
					if (wideBanner == null) return null;

					if (File.Exists(wideBanner.FullImagePath))
						return File.ReadAllBytes(wideBanner.FullImagePath);
					else
					{
						logger.Trace("Could not find TvDB_Banner image: {0}", wideBanner.FullImagePath);
						return null;
					}

				case JMMImageType.TvDB_Episode:

					TvDB_Episode ep = repEpisodes.GetByID(int.Parse(entityID));
					if (ep == null) return null;

					if (File.Exists(ep.FullImagePath))
						return File.ReadAllBytes(ep.FullImagePath);
					else
					{
						logger.Trace("Could not find TvDB_Episode image: {0}", ep.FullImagePath);
						return null;
					}

				case JMMImageType.TvDB_FanArt:

					TvDB_ImageFanart fanart = repFanart.GetByID(int.Parse(entityID));
					if (fanart == null) return null;

					if (thumnbnailOnly)
					{
						if (File.Exists(fanart.FullThumbnailPath))
							return File.ReadAllBytes(fanart.FullThumbnailPath);
						else
						{
							logger.Trace("Could not find TvDB_FanArt image: {0}", fanart.FullThumbnailPath);
							return null;
						}
					}
					else
					{
						if (File.Exists(fanart.FullImagePath))
							return File.ReadAllBytes(fanart.FullImagePath);
						else
						{
							logger.Trace("Could not find TvDB_FanArt image: {0}", fanart.FullImagePath);
							return null;
						}
					}

				case JMMImageType.MovieDB_Poster:

					MovieDB_Poster mPoster = repMoviePosters.GetByID(int.Parse(entityID));
					if (mPoster == null) return null;

					// now find only the original size
                    mPoster = repMoviePosters.GetByOnlineID(mPoster.URL);
					if (mPoster == null) return null;

					if (File.Exists(mPoster.FullImagePath))
						return File.ReadAllBytes(mPoster.FullImagePath);
					else
					{
						logger.Trace("Could not find MovieDB_Poster image: {0}", mPoster.FullImagePath);
						return null;
					}

				case JMMImageType.MovieDB_FanArt:

					MovieDB_Fanart mFanart = repMovieFanart.GetByID(int.Parse(entityID));
					if (mFanart == null) return null;

					mFanart = repMovieFanart.GetByOnlineID(mFanart.URL);
					if (mFanart == null) return null;

					if (File.Exists(mFanart.FullImagePath))
						return File.ReadAllBytes(mFanart.FullImagePath);
					else
					{
						logger.Trace("Could not find MovieDB_FanArt image: {0}", mFanart.FullImagePath);
						return null;
					}

				case JMMImageType.Trakt_Fanart:

					Trakt_ImageFanart tFanart = repTraktFanart.GetByID(int.Parse(entityID));
					if (tFanart == null) return null;

					if (File.Exists(tFanart.FullImagePath))
						return File.ReadAllBytes(tFanart.FullImagePath);
					else
					{
						logger.Trace("Could not find Trakt_Fanart image: {0}", tFanart.FullImagePath);
						return null;
					}

				case JMMImageType.Trakt_Friend:


					Trakt_Friend tFriend = repTraktFriends.GetByID(int.Parse(entityID));
					if (tFriend == null) return null;

					if (File.Exists(tFriend.FullImagePath))
						return File.ReadAllBytes(tFriend.FullImagePath);
					else
					{
						logger.Trace("Could not find Trakt_Friend image: {0}", tFriend.FullImagePath);
						return null;
					}

				case JMMImageType.Trakt_ActivityScrobble:
				case JMMImageType.Trakt_ShoutUser:


					Trakt_Friend tFriendScrobble = repTraktFriends.GetByID(int.Parse(entityID));
					if (tFriendScrobble == null) return null;

					if (File.Exists(tFriendScrobble.FullImagePath))
						return File.ReadAllBytes(tFriendScrobble.FullImagePath);
					else
					{
						logger.Trace("Could not find Trakt_ActivityScrobble image: {0}", tFriendScrobble.FullImagePath);
						return null;
					}

				case JMMImageType.Trakt_Poster:

					Trakt_ImagePoster tPoster = repTraktPosters.GetByID(int.Parse(entityID));
					if (tPoster == null) return null;

					if (File.Exists(tPoster.FullImagePath))
						return File.ReadAllBytes(tPoster.FullImagePath);
					else
					{
						logger.Trace("Could not find Trakt_Poster image: {0}", tPoster.FullImagePath);
						return null;
					}

				case JMMImageType.Trakt_Episode:
				case JMMImageType.Trakt_WatchedEpisode:

					Trakt_Episode tEpisode = repTraktEpisodes.GetByID(int.Parse(entityID));
					if (tEpisode == null) return null;

					if (File.Exists(tEpisode.FullImagePath))
						return File.ReadAllBytes(tEpisode.FullImagePath);
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
				return File.ReadAllBytes(serverImagePath);
			else
			{
				logger.Trace("Could not find AniDB_Cover image: {0}", serverImagePath);
				return null;
			}
		}
	}
}
