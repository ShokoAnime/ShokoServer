using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Xml;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Commons.Extensions;
using Shoko.Commons.Properties;
using Shoko.Commons.Queue;
using Shoko.Commons.Utils;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Extensions;
using Shoko.Server.ImageDownload;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Commands
{
    [Command(CommandRequestType.DownloadAniDBImages)]
    public class CommandRequest_DownloadAniDBImages : CommandRequestImplementation
    {
        public int AnimeID { get; set; }
        public bool ForceDownload { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority1;

        public override QueueStateStruct PrettyDescription => new()
        {
            queueState = QueueStateEnum.DownloadImage,
            extraParams = new[] { Resources.Command_ValidateAllImages_AniDBPosters, AnimeID.ToString() }
        };

        public QueueStateStruct PrettyDescriptionCharacters => new()
        {
            queueState = QueueStateEnum.DownloadImage,
            extraParams = new[] { Resources.Command_ValidateAllImages_AniDBCharacters, AnimeID.ToString() }
        };

        public QueueStateStruct PrettyDescriptionCreators => new()
        {
            queueState = QueueStateEnum.DownloadImage,
            extraParams = new[] { Resources.Command_ValidateAllImages_AniDBSeiyuus, AnimeID.ToString() }
        };

        public CommandRequest_DownloadAniDBImages()
        {
        }

        public CommandRequest_DownloadAniDBImages(int animeID, bool forced)
        {
            AnimeID = animeID;
            ForceDownload = forced;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        protected override void Process(IServiceProvider serviceProvider)
        {
            logger.Info("Processing CommandRequest_DownloadAniDBImages: {AnimeID}", AnimeID);

            try
            {
                var types = new List<ImageEntityType>
                {
                    ImageEntityType.AniDB_Cover,
                    ImageEntityType.AniDB_Character,
                    ImageEntityType.AniDB_Creator
                };
                IUDPConnectionHandler handler = null;
                foreach (var entityTypeEnum in types)
                {
                    var downloadUrls = new List<string>();
                    var fileNames = new List<string>();
                    switch (entityTypeEnum)
                    {
                        case ImageEntityType.AniDB_Cover:
                            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);
                            if (anime == null)
                            {
                                logger.Warn($"AniDB poster image failed to download: Can't find AniDB_Anime with ID: {AnimeID}");
                                return;
                            }

                            handler ??= serviceProvider.GetRequiredService<IUDPConnectionHandler>();
                            downloadUrls.Add(string.Format(handler.CdnUrl, anime.Picname));
                            fileNames.Add(anime.PosterPath);
                            break;

                        case ImageEntityType.AniDB_Character:
                            if (!ServerSettings.Instance.AniDb.DownloadCharacters) continue;
                            var chrs = (from xref1 in RepoFactory.AniDB_Anime_Character.GetByAnimeID(AnimeID)
                                    select RepoFactory.AniDB_Character.GetByCharID(xref1.CharID))
                                .Where(a => !string.IsNullOrEmpty(a?.PicName))
                                .DistinctBy(a => a.CharID)
                                .ToList();
                            if (chrs == null || chrs.Count == 0)
                            {
                                logger.Warn(
                                    $"AniDB Character image failed to download: Can't find Character for anime: {AnimeID}");
                                return;
                            }

                            handler ??= serviceProvider.GetRequiredService<IUDPConnectionHandler>();
                            foreach (var chr in chrs)
                            {
                                downloadUrls.Add(string.Format(handler.CdnUrl, chr.PicName));
                                fileNames.Add(chr.GetPosterPath());
                            }

                            ShokoService.CmdProcessorGeneral.QueueState = PrettyDescriptionCharacters;
                            break;

                        case ImageEntityType.AniDB_Creator:
                            if (!ServerSettings.Instance.AniDb.DownloadCreators) continue;

                            var creators = (from xref1 in RepoFactory.AniDB_Anime_Character.GetByAnimeID(AnimeID)
                                    from xref2 in RepoFactory.AniDB_Character_Seiyuu.GetByCharID(xref1.CharID)
                                    select RepoFactory.AniDB_Seiyuu.GetBySeiyuuID(xref2.SeiyuuID))
                                .Where(a => !string.IsNullOrEmpty(a?.PicName))
                                .DistinctBy(a => a.SeiyuuID)
                                .ToList();
                            if (creators == null || creators.Count == 0)
                            {
                                logger.Warn(
                                    $"AniDB Seiyuu image failed to download: Can't find Seiyuus for anime: {AnimeID}");
                                return;
                            }

                            handler ??= serviceProvider.GetRequiredService<IUDPConnectionHandler>();
                            foreach (var creator in creators)
                            {
                                downloadUrls.Add(string.Format(handler.CdnUrl, creator.PicName));
                                fileNames.Add(creator.GetPosterPath());
                            }

                            ShokoService.CmdProcessorGeneral.QueueState = PrettyDescriptionCreators;
                            break;
                    }

                    if (downloadUrls.Count == 0 || fileNames.All(string.IsNullOrEmpty))
                    {
                        logger.Warn("Image failed to download: No URLs were generated. This should never happen");
                        return;
                    }


                    for (var i = 0; i < downloadUrls.Count; i++)
                    {
                        try
                        {
                            if (string.IsNullOrEmpty(fileNames[i])) continue;
                            var downloadImage = true;
                            var fileExists = File.Exists(fileNames[i]);
                            var imageValid = fileExists && Misc.IsImageValid(fileNames[i]);

                            if (imageValid && !ForceDownload) downloadImage = false;

                            if (!downloadImage) continue;

                            var tempName = Path.Combine(ImageUtils.GetImagesTempFolder(),
                                Path.GetFileName(fileNames[i]));

                            try
                            {
                                if (fileExists) File.Delete(fileNames[i]);
                            }
                            catch (Exception ex)
                            {
                                Thread.CurrentThread.CurrentUICulture =
                                    CultureInfo.GetCultureInfo(ServerSettings.Instance.Culture);

                                logger.Warn(Resources.Command_DeleteError, fileNames, ex.Message);
                                return;
                            }

                            // If this has any issues, it will throw an exception, so the catch below will handle it
                            RecursivelyRetryDownload(downloadUrls[i], ref tempName, 0, 5);

                            // move the file to it's final location
                            // check that the final folder exists
                            var fullPath = Path.GetDirectoryName(fileNames[i]);
                            if (!Directory.Exists(fullPath))
                                Directory.CreateDirectory(fullPath);

                            File.Move(tempName, fileNames[i]);
                            logger.Info($"Image downloaded: {fileNames[i]} from {downloadUrls[i]}");
                        }
                        catch (WebException e)
                        {
                            logger.Warn("Error processing CommandRequest_DownloadAniDBImages: {Url} ({AnimeID}) - {Ex}",
                                downloadUrls[i],
                                AnimeID,
                                e.Message);
                        }catch (Exception e)
                        {
                            logger.Error(e, "Error processing CommandRequest_DownloadAniDBImages: {Url} ({AnimeID}) - {Ex}",
                                downloadUrls[i],
                                AnimeID,
                                e);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error processing CommandRequest_DownloadAniDBImages: {AnimeID} - {Ex}", AnimeID, ex);
            }
        }

        private static void RecursivelyRetryDownload(string downloadURL, ref string tempFilePath, int count, int maxretry)
        {
            try
            {
                // download image
                if (downloadURL.Length <= 0) return;

                // Ignore all certificate failures.
                ServicePointManager.Expect100Continue = true;                
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

                using var client = new WebClient();
                client.Headers.Add("user-agent", "JMM");
                //OnImageDownloadEvent(new ImageDownloadEventArgs("", req, ImageDownloadEventType.Started));
                //BaseConfig.MyAnimeLog.Write("ProcessImages: Download: {0}  *** to ***  {1}", req.URL, fullName);

                AniDbImageRateLimiter.Instance.EnsureRate();
                var bytes = client.DownloadData(downloadURL);
                AniDbImageRateLimiter.Instance.Reset();
                if (bytes.Length < 4)
                    throw new WebException(
                        "The image download stream returned less than 4 bytes (a valid image has 2-4 bytes in the header)");

                var imageFormat = Misc.GetImageFormat(bytes);
                string extension;
                switch (imageFormat)
                {
                    case ImageFormatEnum.bmp:
                        extension = ".bmp";
                        break;
                    case ImageFormatEnum.gif:
                        extension = ".gif";
                        break;
                    case ImageFormatEnum.jpeg:
                        extension = ".jpeg";
                        break;
                    case ImageFormatEnum.png:
                        extension = ".png";
                        break;
                    case ImageFormatEnum.tiff:
                        extension = ".tiff";
                        break;
                    default: throw new WebException("The image download stream returned an invalid image");
                }

                if (extension.Length <= 0) return;
                var newFile = Path.ChangeExtension(tempFilePath, extension);
                if(newFile == null) return;

                if (File.Exists(newFile)) File.Delete(newFile);
                using (var fs = new FileStream(newFile, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(bytes, 0, bytes.Length);
                }
                tempFilePath = newFile;
            }
            catch (WebException)
            {
                if (count + 1 >= maxretry) throw;
                Thread.Sleep(500);
                RecursivelyRetryDownload(downloadURL, ref tempFilePath, count + 1, maxretry);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_DownloadImage_{AnimeID}_{ForceDownload}";
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            Priority = cq.Priority;
            CommandDetails = cq.CommandDetails;
            DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (CommandDetails.Trim().Length <= 0) return true;
            var docCreator = new XmlDocument();
            docCreator.LoadXml(CommandDetails);

            // populate the fields
            AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_DownloadAniDBImages", "AnimeID"));
            ForceDownload =
                bool.Parse(TryGetProperty(docCreator, "CommandRequest_DownloadAniDBImages", "ForceDownload"));

            return true;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            var cq = new CommandRequest
            {
                CommandID = CommandID,
                CommandType = CommandType,
                Priority = Priority,
                CommandDetails = ToXML(),
                DateTimeUpdated = DateTime.Now
            };
            return cq;
        }
    }
}
