using System;
using System.Linq;
using Pri.LongPath;
using Shoko.Commons.Extensions;
using Shoko.Commons.Properties;
using Shoko.Commons.Queue;
using Shoko.Commons.Utils;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.ImageDownload;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    public class CommandRequest_ValidateAllImages : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority3;

        public QueueStateStruct PrettyDescription => new QueueStateStruct()
        {
            queueState = QueueStateEnum.ValidateAllImages,
            extraParams = new string[] {""}
        };

        public CommandRequest_ValidateAllImages()
        {
            this.CommandType = (int) CommandRequestType.ValidateAllImages;
            this.Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_ValidateAllImages");
            try
            {
                QueueStateStruct queueState = PrettyDescription;
                queueState.extraParams = new[] {Resources.Command_ValidateAllImages_TvDBEpisodes};
                ShokoService.CmdProcessorImages.QueueState = queueState;
                RepoFactory.TvDB_Episode.GetAll().Where(fanart =>
                        !string.IsNullOrEmpty(fanart.GetFullImagePath()) &&
                        !Misc.IsImageValid(fanart.GetFullImagePath()))
                    .ForEach(fanart =>
                        RemoveImageAndQueueRedownload(ImageEntityType.TvDB_Episode, fanart.TvDB_EpisodeID, fanart));

                if (ServerSettings.TvDB_AutoFanart)
                {
                    queueState.extraParams = new[] {Resources.Command_ValidateAllImages_TvDBFanarts};
                    ShokoService.CmdProcessorImages.QueueState = queueState;
                    RepoFactory.TvDB_ImageFanart.GetAll().Where(fanart =>
                            !string.IsNullOrEmpty(fanart.GetFullImagePath()) &&
                            !Misc.IsImageValid(fanart.GetFullImagePath()))
                        .ForEach(
                            fanart => RemoveImageAndQueueRedownload(ImageEntityType.TvDB_FanArt,
                                fanart.TvDB_ImageFanartID, fanart));
                }

                if (ServerSettings.TvDB_AutoPosters)
                {
                    queueState.extraParams = new[] {Resources.Command_ValidateAllImages_TvDBPosters};
                    ShokoService.CmdProcessorImages.QueueState = queueState;
                    RepoFactory.TvDB_ImagePoster.GetAll().Where(fanart =>
                            !string.IsNullOrEmpty(fanart.GetFullImagePath()) &&
                            !Misc.IsImageValid(fanart.GetFullImagePath()))
                        .ForEach(fanart =>
                            RemoveImageAndQueueRedownload(ImageEntityType.TvDB_Cover, fanart.TvDB_ImagePosterID,
                                fanart));
                }

                if (ServerSettings.TvDB_AutoWideBanners)
                {
                    queueState.extraParams = new[] {Resources.Command_ValidateAllImages_TvDBBanners};
                    ShokoService.CmdProcessorImages.QueueState = queueState;
                    RepoFactory.TvDB_ImageWideBanner.GetAll()
                        .Where(fanart =>
                            !string.IsNullOrEmpty(fanart.GetFullImagePath()) &&
                            !Misc.IsImageValid(fanart.GetFullImagePath()))
                        .ForEach(
                            fanart => RemoveImageAndQueueRedownload(ImageEntityType.TvDB_Banner,
                                fanart.TvDB_ImageWideBannerID, fanart));
                }

                queueState.extraParams = new[] {Resources.Command_ValidateAllImages_AniDBPosters};
                ShokoService.CmdProcessorImages.QueueState = queueState;
                RepoFactory.AniDB_Anime.GetAll().Where(fanart =>
                        !string.IsNullOrEmpty(fanart.PosterPath) && !Misc.IsImageValid(fanart.PosterPath))
                    .ForEach(fanart =>
                        RemoveImageAndQueueRedownload(ImageEntityType.AniDB_Cover, fanart.AnimeID, fanart));

                if (ServerSettings.AniDB_DownloadCharacters)
                {
                    queueState.extraParams = new[] {Resources.Command_ValidateAllImages_AniDBCharacters};
                    ShokoService.CmdProcessorImages.QueueState = queueState;
                    RepoFactory.AniDB_Character.GetAll().Where(fanart =>
                            !string.IsNullOrEmpty(fanart.GetPosterPath()) && !Misc.IsImageValid(fanart.GetPosterPath()))
                        .ForEach(fanart => RemoveImageAndQueueRedownload(ImageEntityType.AniDB_Character,
                            fanart.AniDB_CharacterID, fanart));
                }

                if (ServerSettings.AniDB_DownloadCreators)
                {
                    queueState.extraParams = new[] {Resources.Command_ValidateAllImages_AniDBSeiyuus};
                    ShokoService.CmdProcessorImages.QueueState = queueState;
                    RepoFactory.AniDB_Seiyuu.GetAll()
                        .Where(fanart =>
                            !string.IsNullOrEmpty(fanart.GetPosterPath()) && !Misc.IsImageValid(fanart.GetPosterPath()))
                        .ForEach(
                            fanart => RemoveImageAndQueueRedownload(ImageEntityType.AniDB_Creator, fanart.SeiyuuID,
                                fanart));
                }
            }
            catch (Exception ex)
            {
                logger.Warn($"Error processing CommandRequest_ValidateAllImages: {ex.Message}");
            }
        }

        private void RemoveImageAndQueueRedownload(ImageEntityType EntityTypeEnum, int EntityID, object Entity)
        {
            CommandRequest_DownloadImage cmd;
            switch (EntityTypeEnum)
            {
                case ImageEntityType.TvDB_Episode:
                    TvDB_Episode episode = Entity as TvDB_Episode;
                    if (episode == null) return;
                    try
                    {
                        if (File.Exists(episode.GetFullImagePath())) File.Delete(episode.GetFullImagePath());
                    }
                    catch
                    {
                    }

                    cmd = new CommandRequest_DownloadImage(EntityID, ImageEntityType.TvDB_Episode, true);
                    break;

                case ImageEntityType.TvDB_FanArt:
                    TvDB_ImageFanart fanart = Entity as TvDB_ImageFanart;
                    if (fanart == null) return;
                    try
                    {
                        if (File.Exists(fanart.GetFullImagePath())) File.Delete(fanart.GetFullImagePath());
                    }
                    catch
                    {
                    }

                    cmd = new CommandRequest_DownloadImage(EntityID, ImageEntityType.TvDB_FanArt, true);
                    break;

                case ImageEntityType.TvDB_Cover:
                    TvDB_ImagePoster poster = Entity as TvDB_ImagePoster;
                    if (poster == null) return;
                    try
                    {
                        if (File.Exists(poster.GetFullImagePath())) File.Delete(poster.GetFullImagePath());
                    }
                    catch
                    {
                    }

                    cmd = new CommandRequest_DownloadImage(EntityID, ImageEntityType.TvDB_Cover, true);
                    break;

                case ImageEntityType.TvDB_Banner:
                    TvDB_ImageWideBanner wideBanner = Entity as TvDB_ImageWideBanner;
                    if (wideBanner == null) return;
                    try
                    {
                        if (File.Exists(wideBanner.GetFullImagePath())) File.Delete(wideBanner.GetFullImagePath());
                    }
                    catch
                    {
                    }

                    cmd = new CommandRequest_DownloadImage(EntityID, ImageEntityType.TvDB_Banner, true);
                    break;

                case ImageEntityType.MovieDB_Poster:
                    MovieDB_Poster moviePoster = Entity as MovieDB_Poster;
                    if (moviePoster == null) return;
                    try
                    {
                        if (File.Exists(moviePoster.GetFullImagePath())) File.Delete(moviePoster.GetFullImagePath());
                    }
                    catch
                    {
                    }

                    cmd = new CommandRequest_DownloadImage(EntityID, ImageEntityType.MovieDB_Poster, true);
                    break;

                case ImageEntityType.MovieDB_FanArt:
                    MovieDB_Fanart movieFanart = Entity as MovieDB_Fanart;
                    if (movieFanart == null) return;
                    try
                    {
                        if (File.Exists(movieFanart.GetFullImagePath())) File.Delete(movieFanart.GetFullImagePath());
                    }
                    catch
                    {
                    }

                    cmd = new CommandRequest_DownloadImage(EntityID, ImageEntityType.MovieDB_FanArt, true);
                    break;
                case ImageEntityType.AniDB_Cover:
                    string coverpath = (Entity as SVR_AniDB_Anime)?.PosterPath;
                    if (string.IsNullOrEmpty(coverpath)) return;
                    try
                    {
                        if (File.Exists(coverpath)) File.Delete(coverpath);
                    }
                    catch
                    {
                    }

                    cmd = new CommandRequest_DownloadImage(EntityID, ImageEntityType.AniDB_Cover, true);
                    break;
                case ImageEntityType.AniDB_Creator:
                    string creatorpath = (Entity as AniDB_Seiyuu)?.GetPosterPath();
                    if (string.IsNullOrEmpty(creatorpath)) return;
                    try
                    {
                        if (File.Exists(creatorpath)) File.Delete(creatorpath);
                    }
                    catch
                    {
                    }

                    cmd = new CommandRequest_DownloadImage(EntityID, ImageEntityType.AniDB_Creator, true);
                    break;
                case ImageEntityType.AniDB_Character:
                    string characterpath = (Entity as AniDB_Character)?.GetPosterPath();
                    if (string.IsNullOrEmpty(characterpath)) return;
                    try
                    {
                        if (File.Exists(characterpath)) File.Delete(characterpath);
                    }
                    catch
                    {
                    }

                    cmd = new CommandRequest_DownloadImage(EntityID, ImageEntityType.AniDB_Character, true);
                    break;
                default:
                    return;
            }
            cmd.Save();
        }

        public override void GenerateCommandID()
        {
            this.CommandID = $"CommandRequest_ValidateAllImages";
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            this.CommandID = cq.CommandID;
            this.CommandRequestID = cq.CommandRequestID;
            this.CommandType = cq.CommandType;
            this.Priority = cq.Priority;
            this.CommandDetails = cq.CommandDetails;
            this.DateTimeUpdated = cq.DateTimeUpdated;

            return true;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            CommandRequest cq = new CommandRequest
            {
                CommandID = this.CommandID,
                CommandType = this.CommandType,
                Priority = this.Priority,
                CommandDetails = this.ToXML(),
                DateTimeUpdated = DateTime.Now
            };
            return cq;
        }
    }
}