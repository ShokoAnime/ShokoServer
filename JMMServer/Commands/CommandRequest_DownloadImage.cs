using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Repositories;
using JMMServer.ImageDownload;
using System.IO;
using System.Net;

namespace JMMServer.Commands
{
	public class CommandRequest_DownloadImage : CommandRequestImplementation, ICommandRequest
	{
		public int EntityID { get; set; }
		public int EntityType { get; set; }
		public bool ForceDownload { get; set; }

		public JMMImageType EntityTypeEnum
		{
			get
			{
				return (JMMImageType)EntityType;
			}
		}

		public CommandRequestPriority DefaultPriority 
		{
			get { return CommandRequestPriority.Priority2; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Downloading Image: {0}", EntityID);
			}
		}

		public CommandRequest_DownloadImage()
		{
		}

		public CommandRequest_DownloadImage(int entityID, JMMImageType entityType, bool forced)
		{
			this.EntityID = entityID;
			this.EntityType = (int)entityType;
			this.ForceDownload = forced;
			this.CommandType = (int)CommandRequestType.ImageDownload;
			this.Priority = (int)DefaultPriority;

			GenerateCommandID();
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_DownloadImage: {0}", EntityID);

			try
			{
				ImageDownloadRequest req = null;
				switch (EntityTypeEnum)
				{
					case JMMImageType.AniDB_Cover:
						AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
						AniDB_Anime anime = repAnime.GetByID(EntityID);
						if (anime == null) return;

						req = new ImageDownloadRequest(EntityTypeEnum, anime, ForceDownload);
						break;

					case JMMImageType.TvDB_Episode:

						TvDB_EpisodeRepository repTvEp = new TvDB_EpisodeRepository();
						TvDB_Episode ep = repTvEp.GetByID(EntityID);
						if (ep == null) return;
						if (string.IsNullOrEmpty(ep.Filename)) return;

						req = new ImageDownloadRequest(EntityTypeEnum, ep, ForceDownload);
						break;

					case JMMImageType.TvDB_FanArt:

						TvDB_ImageFanartRepository repFanart = new TvDB_ImageFanartRepository();
						TvDB_ImageFanart fanart = repFanart.GetByID(EntityID);
						if (fanart == null) return;
						if (string.IsNullOrEmpty(fanart.BannerPath)) return;

						req = new ImageDownloadRequest(EntityTypeEnum, fanart, ForceDownload);
						break;

					case JMMImageType.TvDB_Cover:

						TvDB_ImagePosterRepository repPoster = new TvDB_ImagePosterRepository();
						TvDB_ImagePoster poster = repPoster.GetByID(EntityID);
						if (poster == null) return;
						if (string.IsNullOrEmpty(poster.BannerPath)) return;

						req = new ImageDownloadRequest(EntityTypeEnum, poster, ForceDownload);
						break;

					case JMMImageType.TvDB_Banner:

						TvDB_ImageWideBannerRepository repBanners = new TvDB_ImageWideBannerRepository();
						TvDB_ImageWideBanner wideBanner = repBanners.GetByID(EntityID);
						if (wideBanner == null) return;
						if (string.IsNullOrEmpty(wideBanner.BannerPath)) return;

						req = new ImageDownloadRequest(EntityTypeEnum, wideBanner, ForceDownload);
						break;

					case JMMImageType.MovieDB_Poster:

						MovieDB_PosterRepository repMoviePosters = new MovieDB_PosterRepository();
						MovieDB_Poster moviePoster = repMoviePosters.GetByID(EntityID);
						if (moviePoster == null) return;
						if (string.IsNullOrEmpty(moviePoster.URL)) return;

						req = new ImageDownloadRequest(EntityTypeEnum, moviePoster, ForceDownload);
						break;

					case JMMImageType.MovieDB_FanArt:

						MovieDB_FanartRepository repMovieFanart = new MovieDB_FanartRepository();
						MovieDB_Fanart movieFanart = repMovieFanart.GetByID(EntityID);
						if (movieFanart == null) return;
						if (string.IsNullOrEmpty(movieFanart.URL)) return;

						req = new ImageDownloadRequest(EntityTypeEnum, movieFanart, ForceDownload);
						break;

					case JMMImageType.Trakt_Poster:

						Trakt_ImagePosterRepository repTraktPosters = new Trakt_ImagePosterRepository();
						Trakt_ImagePoster traktPoster = repTraktPosters.GetByID(EntityID);
						if (traktPoster == null) return;
						if (string.IsNullOrEmpty(traktPoster.ImageURL)) return;

						req = new ImageDownloadRequest(EntityTypeEnum, traktPoster, ForceDownload);
						break;

					case JMMImageType.Trakt_Fanart:

						Trakt_ImageFanartRepository repTraktFanarts = new Trakt_ImageFanartRepository();
						Trakt_ImageFanart traktFanart = repTraktFanarts.GetByID(EntityID);
						if (traktFanart == null) return;
						if (string.IsNullOrEmpty(traktFanart.ImageURL)) return;

						req = new ImageDownloadRequest(EntityTypeEnum, traktFanart, ForceDownload);
						break;

					case JMMImageType.Trakt_Episode:

						Trakt_EpisodeRepository repTraktEpisodes = new Trakt_EpisodeRepository();
						Trakt_Episode traktEp = repTraktEpisodes.GetByID(EntityID);
						if (traktEp == null) return;
						if (string.IsNullOrEmpty(traktEp.EpisodeImage)) return;

						req = new ImageDownloadRequest(EntityTypeEnum, traktEp, ForceDownload);
						break;

					case JMMImageType.AniDB_Character:
						AniDB_CharacterRepository repChars = new AniDB_CharacterRepository();
						AniDB_Character chr = repChars.GetByID(EntityID);
						if (chr == null) return;

						req = new ImageDownloadRequest(EntityTypeEnum, chr, ForceDownload);
						break;

					case JMMImageType.AniDB_Creator:
						AniDB_CreatorRepository repCreator = new AniDB_CreatorRepository();
						AniDB_Creator creator = repCreator.GetByID(EntityID);
						if (creator == null) return;

						req = new ImageDownloadRequest(EntityTypeEnum, creator, ForceDownload);
						break;
				}

				if (req == null) return;

				List<string> fileNames = new List<string>();
				List<string> downloadURLs = new List<string>();

				string fileNameTemp = GetFileName(req, false);
				string downloadURLTemp = GetFileURL(req, false);

				fileNames.Add(fileNameTemp);
				downloadURLs.Add(downloadURLTemp);

				if (req.ImageType == JMMImageType.TvDB_FanArt)
				{
					fileNameTemp = GetFileName(req, true);
					downloadURLTemp = GetFileURL(req, true);

					fileNames.Add(fileNameTemp);
					downloadURLs.Add(downloadURLTemp);
				}

				for (int i = 0; i < fileNames.Count; i++)
				{
					string fileName = fileNames[i];
					string downloadURL = downloadURLs[i];

					bool downloadImage = true;
					bool fileExists = File.Exists(fileName);

					if (fileExists)
					{
						if (!req.ForceDownload)
							downloadImage = false;
						else
							downloadImage = true;
					}
					else
						downloadImage = true;

					if (downloadImage)
					{
						string tempName = Path.Combine(ImageUtils.GetImagesTempFolder(), Path.GetFileName(fileName));
						if (File.Exists(tempName)) File.Delete(tempName);


						if (fileExists) File.Delete(fileName);

						// download image
						using (WebClient client = new WebClient())
						{
							client.Headers.Add("user-agent", "JMM");
							//OnImageDownloadEvent(new ImageDownloadEventArgs("", req, ImageDownloadEventType.Started));
							//BaseConfig.MyAnimeLog.Write("ProcessImages: Download: {0}  *** to ***  {1}", req.URL, fullName);
							if (downloadURL.Length > 0)
							{
								client.DownloadFile(downloadURL, tempName);

								string extension = "";
								string contentType = client.ResponseHeaders["Content-type"].ToLower();
								if (contentType.IndexOf("gif") >= 0) extension = ".gif";
								if (contentType.IndexOf("jpg") >= 0) extension = ".jpg";
								if (contentType.IndexOf("jpeg") >= 0) extension = ".jpg";
								if (contentType.IndexOf("bmp") >= 0) extension = ".bmp";
								if (contentType.IndexOf("png") >= 0) extension = ".png";
								if (extension.Length > 0)
								{
									string newFile = Path.ChangeExtension(tempName, extension);
									if (!newFile.ToLower().Equals(tempName.ToLower()))
									{
										try
										{
											System.IO.File.Delete(newFile);
										}
										catch
										{
											//BaseConfig.MyAnimeLog.Write("DownloadedImage:Download() Delete failed:{0}", newFile);
										}
										System.IO.File.Move(tempName, newFile);
										tempName = newFile;
									}
								}
							}
						}

						// move the file to it's final location
						// check that the final folder exists
						string fullPath = Path.GetDirectoryName(fileName);
						if (!Directory.Exists(fullPath))
							Directory.CreateDirectory(fullPath);


						System.IO.File.Move(tempName, fileName);
						logger.Info("Image downloaded: {0}", fileName);
					}
				}

			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_DownloadImage: {0} - {1}", EntityID, ex.ToString());
				return;
			}
		}

		public static string GetFileURL(ImageDownloadRequest req, bool thumbNailOnly)
		{
			switch (req.ImageType)
			{
				case JMMImageType.AniDB_Cover:
					AniDB_Anime anime = req.ImageData as AniDB_Anime;
					return string.Format(Constants.URLS.AniDB_Images, anime.Picname);

				case JMMImageType.TvDB_Episode:
					TvDB_Episode ep = req.ImageData as TvDB_Episode;
					return string.Format(Constants.URLS.TvDB_Images, ep.Filename);

				case JMMImageType.TvDB_FanArt:
					TvDB_ImageFanart fanart = req.ImageData as TvDB_ImageFanart;

					if (thumbNailOnly)
						return string.Format(Constants.URLS.TvDB_Images, fanart.ThumbnailPath);
					else
						return string.Format(Constants.URLS.TvDB_Images, fanart.BannerPath);

				case JMMImageType.TvDB_Cover:
					TvDB_ImagePoster poster = req.ImageData as TvDB_ImagePoster;
					return string.Format(Constants.URLS.TvDB_Images, poster.BannerPath);

				case JMMImageType.TvDB_Banner:
					TvDB_ImageWideBanner wideBanner = req.ImageData as TvDB_ImageWideBanner;
					return string.Format(Constants.URLS.TvDB_Images, wideBanner.BannerPath);

				case JMMImageType.MovieDB_Poster:
					MovieDB_Poster moviePoster = req.ImageData as MovieDB_Poster;
					return moviePoster.URL;

				case JMMImageType.MovieDB_FanArt:

					MovieDB_Fanart movieFanart = req.ImageData as MovieDB_Fanart;
					return movieFanart.URL;

				case JMMImageType.Trakt_Poster:
					Trakt_ImagePoster traktPoster = req.ImageData as Trakt_ImagePoster;
					return traktPoster.ImageURL;

				case JMMImageType.Trakt_Fanart:
					Trakt_ImageFanart traktFanart = req.ImageData as Trakt_ImageFanart;
					return traktFanart.ImageURL;

				case JMMImageType.Trakt_Episode:
					Trakt_Episode traktEp = req.ImageData as Trakt_Episode;
					return traktEp.EpisodeImage;

				case JMMImageType.AniDB_Character:
					AniDB_Character chr = req.ImageData as AniDB_Character;
					return string.Format(Constants.URLS.AniDB_Images, chr.PicName);

				case JMMImageType.AniDB_Creator:
					AniDB_Creator creator = req.ImageData as AniDB_Creator;
					return string.Format(Constants.URLS.AniDB_Images, creator.PicName);

				default:
					return "";

			}
		}

		private string GetFileName(ImageDownloadRequest req, bool thumbNailOnly)
		{
			switch (req.ImageType)
			{
				case JMMImageType.AniDB_Cover:

					AniDB_Anime anime = req.ImageData as AniDB_Anime;
					return anime.PosterPath;

				case JMMImageType.TvDB_Episode:

					TvDB_Episode ep = req.ImageData as TvDB_Episode;
					return ep.FullImagePath;

				case JMMImageType.TvDB_FanArt:

					TvDB_ImageFanart fanart = req.ImageData as TvDB_ImageFanart;
					if (thumbNailOnly)
						return fanart.FullThumbnailPath;
					else
						return fanart.FullImagePath;

				case JMMImageType.TvDB_Cover:

					TvDB_ImagePoster poster = req.ImageData as TvDB_ImagePoster;
					return poster.FullImagePath;

				case JMMImageType.TvDB_Banner:

					TvDB_ImageWideBanner wideBanner = req.ImageData as TvDB_ImageWideBanner;
					return wideBanner.FullImagePath;

				case JMMImageType.MovieDB_Poster:

					MovieDB_Poster moviePoster = req.ImageData as MovieDB_Poster;
					return moviePoster.FullImagePath;

				case JMMImageType.MovieDB_FanArt:

					MovieDB_Fanart movieFanart = req.ImageData as MovieDB_Fanart;
					return movieFanart.FullImagePath;

				case JMMImageType.Trakt_Poster:
					Trakt_ImagePoster traktPoster = req.ImageData as Trakt_ImagePoster;
					return traktPoster.FullImagePath;

				case JMMImageType.Trakt_Fanart:
					Trakt_ImageFanart traktFanart = req.ImageData as Trakt_ImageFanart;
					return traktFanart.FullImagePath;

				case JMMImageType.Trakt_Episode:
					Trakt_Episode traktEp = req.ImageData as Trakt_Episode;
					return traktEp.FullImagePath;

				case JMMImageType.AniDB_Character:
					AniDB_Character chr = req.ImageData as AniDB_Character;
					return chr.PosterPath;

				case JMMImageType.AniDB_Creator:
					AniDB_Creator creator = req.ImageData as AniDB_Creator;
					return creator.PosterPath;

				default:
					return "";
			}

		}

		public override void GenerateCommandID()
		{
			this.CommandID = string.Format("CommandRequest_DownloadImage_{0}_{1}", EntityID, (int)EntityType);
		}

		public override bool LoadFromDBCommand(CommandRequest cq)
		{
			this.CommandID = cq.CommandID;
			this.CommandRequestID = cq.CommandRequestID;
			this.CommandType = cq.CommandType;
			this.Priority = cq.Priority;
			this.CommandDetails = cq.CommandDetails;
			this.DateTimeUpdated = cq.DateTimeUpdated;

			// read xml to get parameters
			if (this.CommandDetails.Trim().Length > 0)
			{
				XmlDocument docCreator = new XmlDocument();
				docCreator.LoadXml(this.CommandDetails);

				// populate the fields
				this.EntityID = int.Parse(TryGetProperty(docCreator, "CommandRequest_DownloadImage", "EntityID"));
				this.EntityType = int.Parse(TryGetProperty(docCreator, "CommandRequest_DownloadImage", "EntityType"));
				this.ForceDownload = bool.Parse(TryGetProperty(docCreator, "CommandRequest_DownloadImage", "ForceDownload"));
			}

			return true;
		}

		public override CommandRequest ToDatabaseObject()
		{
			GenerateCommandID();

			CommandRequest cq = new CommandRequest();
			cq.CommandID = this.CommandID;
			cq.CommandType = this.CommandType;
			cq.Priority = this.Priority;
			cq.CommandDetails = this.ToXML();
			cq.DateTimeUpdated = DateTime.Now;

			return cq;
		}
	}
}
