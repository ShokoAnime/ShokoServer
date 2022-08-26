using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Properties;
using Shoko.Commons.Queue;
using Shoko.Commons.Utils;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Commands
{
    [Command(CommandRequestType.ValidateAllImages)]
    public class CommandRequest_ValidateAllImages : CommandRequestImplementation
    {
        private readonly ICommandRequestFactory _commandFactory;
        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority3;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            message = "Validating Images {0}",
            queueState = QueueStateEnum.ValidateAllImages,
            extraParams = new[] {string.Empty}
        };

        protected override void Process()
        {
            Logger.LogInformation("Processing CommandRequest_ValidateAllImages");
            try
            {
                var queueState = PrettyDescription;
                queueState.extraParams = new[] {Resources.Command_ValidateAllImages_TvDBEpisodes};
                ShokoService.CmdProcessorImages.QueueState = queueState;
                var count = 0;
                Logger.LogInformation("Scanning TvDB Episode thumbs for corrupted images");
                var episodes = RepoFactory.TvDB_Episode.GetAll().Where(fanart =>
                        !string.IsNullOrEmpty(fanart.GetFullImagePath()) &&
                        !Misc.IsImageValid(fanart.GetFullImagePath())).ToList();

                Logger.LogInformation("Found {Count} corrupted TvDB Episode {Thumbs}", episodes.Count, (episodes.Count == 1 ? "thumb" : "thumbs"));
                foreach (var fanart in episodes)
                {
                    Logger.LogTrace("Corrupt image found! Attempting Redownload: {FullImagePath}", fanart.GetFullImagePath());
                    RemoveImageAndQueueRedownload(ImageEntityType.TvDB_Episode, fanart.TvDB_EpisodeID, fanart);
                    count++;
                    if (count % 10 == 0)
                    {
                        Logger.LogInformation("Deleting and queueing for redownload {Count}/{EpisodesCount}", count, episodes.Count);
                        queueState.extraParams = new[]
                            {$"{Resources.Command_ValidateAllImages_TvDBEpisodes} - {count}/{episodes.Count}"};
                        ShokoService.CmdProcessorImages.QueueState = queueState;
                    }
                }

                if (ServerSettings.Instance.TvDB.AutoFanart)
                {
                    count = 0;
                    queueState.extraParams = new[] {Resources.Command_ValidateAllImages_TvDBFanarts};
                    ShokoService.CmdProcessorImages.QueueState = queueState;
                    Logger.LogInformation("Scanning TvDB Fanarts for corrupted images");
                    var fanarts = RepoFactory.TvDB_ImageFanart.GetAll().Where(fanart =>
                        !string.IsNullOrEmpty(fanart.GetFullImagePath()) &&
                        !Misc.IsImageValid(fanart.GetFullImagePath())).ToList();

                    Logger.LogInformation("Found {FanartsCount} corrupted TvDB {Fanarts}", fanarts.Count, (fanarts.Count == 1 ? "Fanart" : "Fanarts"));
                    foreach (var fanart in fanarts)
                    {
                        Logger.LogTrace("Corrupt image found! Attempting Redownload: {FullImagePath}", fanart.GetFullImagePath());
                        RemoveImageAndQueueRedownload(ImageEntityType.TvDB_FanArt, fanart.TvDB_ImageFanartID, fanart);
                        count++;
                        if (count % 10 == 0)
                        {
                            Logger.LogInformation("Deleting and queueing for redownload {Count}/{FanartsCount}", count, fanarts.Count);
                            queueState.extraParams = new[]
                                {$"{Resources.Command_ValidateAllImages_TvDBFanarts} - {count}/{fanarts.Count}"};
                            ShokoService.CmdProcessorImages.QueueState = queueState;
                        }
                    }
                }

                if (ServerSettings.Instance.TvDB.AutoPosters)
                {
                    count = 0;
                    queueState.extraParams = new[] {Resources.Command_ValidateAllImages_TvDBPosters};
                    ShokoService.CmdProcessorImages.QueueState = queueState;
                    Logger.LogInformation("Scanning TvDB Posters for corrupted images");
                    var fanarts = RepoFactory.TvDB_ImagePoster.GetAll().Where(fanart =>
                            !string.IsNullOrEmpty(fanart.GetFullImagePath()) &&
                            !Misc.IsImageValid(fanart.GetFullImagePath())).ToList();

                    Logger.LogInformation("Found {FanartsCount} corrupted TvDB {Posters}", fanarts.Count, (fanarts.Count == 1 ? "Poster" : "Posters"));
                    foreach (var fanart in fanarts)
                    {
                        Logger.LogTrace("Corrupt image found! Attempting Redownload: {FullImagePath}", fanart.GetFullImagePath());
                        RemoveImageAndQueueRedownload(ImageEntityType.TvDB_Cover, fanart.TvDB_ImagePosterID, fanart);
                        count++;
                        if (count % 10 == 0)
                        {
                            Logger.LogInformation("Deleting and queueing for redownload {Count}/{FanartsCount}", count, fanarts.Count);
                            queueState.extraParams = new[]
                                {$"{Resources.Command_ValidateAllImages_TvDBPosters} - {count}/{fanarts.Count}"};
                            ShokoService.CmdProcessorImages.QueueState = queueState;
                        }
                    }
                }

                if (ServerSettings.Instance.TvDB.AutoWideBanners)
                {
                    count = 0;
                    Logger.LogInformation("Scanning TvDB Banners for corrupted images");
                    queueState.extraParams = new[] {Resources.Command_ValidateAllImages_TvDBBanners};
                    ShokoService.CmdProcessorImages.QueueState = queueState;
                    var fanarts = RepoFactory.TvDB_ImageWideBanner.GetAll().Where(fanart =>
                        !string.IsNullOrEmpty(fanart.GetFullImagePath()) &&
                        !Misc.IsImageValid(fanart.GetFullImagePath())).ToList();

                    Logger.LogInformation("Found {FanartsCount} corrupted TvDB {Banners}", fanarts.Count, (fanarts.Count == 1 ? "Banner" : "Banners"));
                    foreach (var fanart in fanarts)
                    {
                        Logger.LogTrace("Corrupt image found! Attempting Redownload: {FullImagePath}", fanart.GetFullImagePath());
                        RemoveImageAndQueueRedownload(ImageEntityType.TvDB_Banner, fanart.TvDB_ImageWideBannerID, fanart);
                        count++;
                        if (count % 10 == 0)
                        {
                            Logger.LogInformation("Deleting and queueing for redownload {Count}/{FanartsCount}", count, fanarts.Count);
                            queueState.extraParams = new[]
                                {$"{Resources.Command_ValidateAllImages_TvDBBanners} - {count}/{fanarts.Count}"};
                            ShokoService.CmdProcessorImages.QueueState = queueState;
                        }
                    }
                }

                if (ServerSettings.Instance.MovieDb.AutoPosters)
                {
                    queueState.extraParams = new[] {Resources.Command_ValidateAllImages_MovieDBPosters};
                    ShokoService.CmdProcessorImages.QueueState = queueState;
                    count = 0;
                    Logger.LogInformation("Scanning MovieDB Posters for corrupted images");
                    var fanarts = RepoFactory.MovieDB_Poster.GetAll().Where(fanart =>
                        !string.IsNullOrEmpty(fanart.GetFullImagePath()) &&
                        !Misc.IsImageValid(fanart.GetFullImagePath())).ToList();

                    Logger.LogInformation("Found {FanartsCount} corrupted MovieDB {Posters}", fanarts.Count, (fanarts.Count == 1 ? "Poster" : "Posters"));
                    foreach (var fanart in fanarts)
                    {
                        Logger.LogTrace("Corrupt image found! Attempting Redownload: {FullImagePath}", fanart.GetFullImagePath());
                        RemoveImageAndQueueRedownload(ImageEntityType.MovieDB_Poster, fanart.MovieDB_PosterID, fanart);
                        count++;
                        if (count % 10 == 0)
                        {
                            Logger.LogInformation("Deleting and queueing for redownload {Count}/{FanartsCount}", count, fanarts.Count);
                            queueState.extraParams = new[]
                                {$"{Resources.Command_ValidateAllImages_MovieDBPosters} - {count}/{fanarts.Count}"};
                            ShokoService.CmdProcessorImages.QueueState = queueState;
                        }
                    }
                }

                if (ServerSettings.Instance.MovieDb.AutoFanart)
                {
                    queueState.extraParams = new[] {Resources.Command_ValidateAllImages_MovieDBFanarts};
                    ShokoService.CmdProcessorImages.QueueState = queueState;
                    count = 0;
                    Logger.LogInformation("Scanning MovieDB Fanarts for corrupted images");
                    var fanarts = RepoFactory.MovieDB_Fanart.GetAll().Where(fanart =>
                            !string.IsNullOrEmpty(fanart.GetFullImagePath()) &&
                            !Misc.IsImageValid(fanart.GetFullImagePath())).ToList();
                    Logger.LogInformation("Found {FanartsCount} corrupted MovieDB {Fanarts}", fanarts.Count, (fanarts.Count == 1 ? "Fanart" : "Fanarts"));
                    foreach (var fanart in fanarts)
                    {
                        Logger.LogTrace("Corrupt image found! Attempting Redownload: {FullImagePath}", fanart.GetFullImagePath());
                        RemoveImageAndQueueRedownload(ImageEntityType.MovieDB_FanArt, fanart.MovieDB_FanartID, fanart);
                        count++;
                        if (count % 10 == 0)
                        {
                            Logger.LogInformation("Deleting and queueing for redownload {Count}/{FanartsCount}", count, fanarts.Count);
                            queueState.extraParams = new[]
                                {$"{Resources.Command_ValidateAllImages_MovieDBFanarts} - {count}/{fanarts.Count}"};
                            ShokoService.CmdProcessorImages.QueueState = queueState;
                        }
                    }
                }

                queueState.extraParams = new[] {Resources.Command_ValidateAllImages_AniDBPosters};
                ShokoService.CmdProcessorImages.QueueState = queueState;
                count = 0;
                Logger.LogInformation("Scanning AniDB Posters for corrupted images");
                var posters = RepoFactory.AniDB_Anime.GetAll().Where(fanart =>
                        !string.IsNullOrEmpty(fanart.PosterPath) && !Misc.IsImageValid(fanart.PosterPath)).ToList();
                Logger.LogInformation("Found {PostersCount} corrupted AniDB {Posters}", posters.Count, (posters.Count == 1 ? "Poster" : "Posters"));
                foreach (var fanart in posters)
                {
                    Logger.LogTrace("Corrupt image found! Attempting Redownload: {FanartPosterPath}", fanart.PosterPath);
                    RemoveImageAndQueueRedownload(ImageEntityType.AniDB_Cover, fanart.AnimeID, fanart);
                    count++;
                    if (count % 10 == 0)
                    {
                        Logger.LogInformation("Deleting and queueing for redownload {Count}/{PostersCount}", count, posters.Count);
                        queueState.extraParams = new[]
                            {$"{Resources.Command_ValidateAllImages_AniDBPosters} - {count}/{posters.Count}"};
                        ShokoService.CmdProcessorImages.QueueState = queueState;
                    }
                }

                if (ServerSettings.Instance.AniDb.DownloadCharacters)
                {
                    queueState.extraParams = new[] {Resources.Command_ValidateAllImages_AniDBCharacters};
                    ShokoService.CmdProcessorImages.QueueState = queueState;
                    count = 0;
                    Logger.LogInformation("Scanning AniDB Characters for corrupted images");
                    var fanarts = RepoFactory.AniDB_Character.GetAll().Where(fanart =>
                            !string.IsNullOrEmpty(fanart.GetPosterPath()) && !Misc.IsImageValid(fanart.GetPosterPath()))
                        .ToList();
                    Logger.LogInformation("Found {FanartsCount} corrupted AniDB Character {Images}", fanarts.Count, (fanarts.Count == 1 ? "image" : "images"));
                    foreach (var fanart in fanarts)
                    {
                        Logger.LogTrace("Corrupt image found! Attempting Redownload: {PosterPath}", fanart.GetPosterPath());
                        RemoveImageAndQueueRedownload(ImageEntityType.AniDB_Character, fanart.AniDB_CharacterID, fanart);
                        count++;
                        if (count % 10 == 0)
                        {
                            Logger.LogInformation("Deleting and queueing for redownload {Count}/{FanartsCount}", count, fanarts.Count);
                            queueState.extraParams = new[]
                                {$"{Resources.Command_ValidateAllImages_AniDBCharacters} - {count}/{fanarts.Count}"};
                            ShokoService.CmdProcessorImages.QueueState = queueState;
                        }
                    }
                }

                if (ServerSettings.Instance.AniDb.DownloadCreators)
                {
                    queueState.extraParams = new[] {Resources.Command_ValidateAllImages_AniDBSeiyuus};
                    ShokoService.CmdProcessorImages.QueueState = queueState;
                    count = 0;
                    Logger.LogInformation("Scanning AniDB Seiyuus for corrupted images");
                    var fanarts = RepoFactory.AniDB_Seiyuu.GetAll().Where(fanart =>
                        !string.IsNullOrEmpty(fanart.GetPosterPath()) && !Misc.IsImageValid(fanart.GetPosterPath())).ToList();
                    Logger.LogInformation("Found {FanartsCount} corrupted AniDB Seiyuu {Images}", fanarts.Count, (fanarts.Count == 1 ? "image" : "images"));
                    foreach (var fanart in fanarts)
                    {
                        Logger.LogTrace("Corrupt image found! Attempting Redownload: {PosterPath}", fanart.GetPosterPath());
                        RemoveImageAndQueueRedownload(ImageEntityType.AniDB_Creator, fanart.SeiyuuID, fanart);
                        count++;
                        if (count % 10 == 0)
                        {
                            Logger.LogInformation("Deleting and queueing for redownload {Count}/{FanartsCount}", count, fanarts.Count);
                            queueState.extraParams = new[]
                                {$"{Resources.Command_ValidateAllImages_AniDBSeiyuus} - {count}/{fanarts.Count}"};
                            ShokoService.CmdProcessorImages.QueueState = queueState;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Error processing CommandRequest_ValidateAllImages: {Message}", ex.Message);
            }
        }

        private void RemoveImageAndQueueRedownload(ImageEntityType entityTypeEnum, int entityID, object entity)
        {
            CommandRequest_DownloadImage cmd;
            switch (entityTypeEnum)
            {
                case ImageEntityType.TvDB_Episode:
                    var episode = entity as TvDB_Episode;
                    if (episode == null) return;
                    try
                    {
                        if (File.Exists(episode.GetFullImagePath())) File.Delete(episode.GetFullImagePath());
                    }
                    catch (Exception e)
                    {
                        Logger.LogError("Unable to delete {FullImagePath} - {Message}", episode.GetFullImagePath(), e.Message);
                    }

                    cmd = _commandFactory.Create<CommandRequest_DownloadImage>(
                        c =>
                        {
                            c.EntityID = entityID;
                            c.EntityType = (int)ImageEntityType.TvDB_Episode;
                            c.ForceDownload = true;
                        }
                    );
                    break;

                case ImageEntityType.TvDB_FanArt:
                    var fanart = entity as TvDB_ImageFanart;
                    if (fanart == null) return;
                    try
                    {
                        if (File.Exists(fanart.GetFullImagePath())) File.Delete(fanart.GetFullImagePath());
                    }
                    catch (Exception e)
                    {
                        Logger.LogError("Unable to delete {FullImagePath} - {Message}", fanart.GetFullImagePath(), e.Message);
                    }

                    cmd = _commandFactory.Create<CommandRequest_DownloadImage>(
                        c =>
                        {
                            c.EntityID = entityID;
                            c.EntityType = (int)ImageEntityType.TvDB_FanArt;
                            c.ForceDownload = true;
                        }
                    );
                    break;

                case ImageEntityType.TvDB_Cover:
                    var poster = entity as TvDB_ImagePoster;
                    if (poster == null) return;
                    try
                    {
                        if (File.Exists(poster.GetFullImagePath())) File.Delete(poster.GetFullImagePath());
                    }
                    catch (Exception e)
                    {
                        Logger.LogError("Unable to delete {FullImagePath} - {Message}", poster.GetFullImagePath(), e.Message);
                    }

                    cmd = _commandFactory.Create<CommandRequest_DownloadImage>(
                        c =>
                        {
                            c.EntityID = entityID;
                            c.EntityType = (int)ImageEntityType.TvDB_Cover;
                            c.ForceDownload = true;
                        }
                    );
                    break;

                case ImageEntityType.TvDB_Banner:
                    var wideBanner = entity as TvDB_ImageWideBanner;
                    if (wideBanner == null) return;
                    try
                    {
                        if (File.Exists(wideBanner.GetFullImagePath())) File.Delete(wideBanner.GetFullImagePath());
                    }
                    catch (Exception e)
                    {
                        Logger.LogError("Unable to delete {FullImagePath} - {Message}", wideBanner.GetFullImagePath(), e.Message);
                    }

                    cmd = _commandFactory.Create<CommandRequest_DownloadImage>(
                        c =>
                        {
                            c.EntityID = entityID;
                            c.EntityType = (int)ImageEntityType.TvDB_Banner;
                            c.ForceDownload = true;
                        }
                    );
                    break;

                case ImageEntityType.MovieDB_Poster:
                    var moviePoster = entity as MovieDB_Poster;
                    if (moviePoster == null) return;
                    try
                    {
                        if (File.Exists(moviePoster.GetFullImagePath())) File.Delete(moviePoster.GetFullImagePath());
                    }
                    catch (Exception e)
                    {
                        Logger.LogError("Unable to delete {FullImagePath} - {Message}", moviePoster.GetFullImagePath(), e.Message);
                    }

                    cmd = _commandFactory.Create<CommandRequest_DownloadImage>(
                        c =>
                        {
                            c.EntityID = entityID;
                            c.EntityType = (int)ImageEntityType.MovieDB_Poster;
                            c.ForceDownload = true;
                        }
                    );
                    break;

                case ImageEntityType.MovieDB_FanArt:
                    var movieFanart = entity as MovieDB_Fanart;
                    if (movieFanart == null) return;
                    try
                    {
                        if (File.Exists(movieFanart.GetFullImagePath())) File.Delete(movieFanart.GetFullImagePath());
                    }
                    catch (Exception e)
                    {
                        Logger.LogError("Unable to delete {FullImagePath} - {Message}", movieFanart.GetFullImagePath(), e.Message);
                    }

                    cmd = _commandFactory.Create<CommandRequest_DownloadImage>(
                        c =>
                        {
                            c.EntityID = entityID;
                            c.EntityType = (int)ImageEntityType.MovieDB_FanArt;
                            c.ForceDownload = true;
                        }
                    );
                    break;
                case ImageEntityType.AniDB_Cover:
                    var coverpath = (entity as SVR_AniDB_Anime)?.PosterPath;
                    if (string.IsNullOrEmpty(coverpath)) return;
                    try
                    {
                        if (File.Exists(coverpath)) File.Delete(coverpath);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError("Unable to delete {Coverpath} - {Message}", coverpath, e.Message);
                    }

                    cmd = _commandFactory.Create<CommandRequest_DownloadImage>(
                        c =>
                        {
                            c.EntityID = entityID;
                            c.EntityType = (int)ImageEntityType.AniDB_Cover;
                            c.ForceDownload = true;
                        }
                    );
                    break;
                case ImageEntityType.AniDB_Creator:
                    var creatorpath = (entity as AniDB_Seiyuu)?.GetPosterPath();
                    if (string.IsNullOrEmpty(creatorpath)) return;
                    try
                    {
                        if (File.Exists(creatorpath)) File.Delete(creatorpath);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError("Unable to delete {Creatorpath} - {Message}", creatorpath, e.Message);
                    }

                    cmd = _commandFactory.Create<CommandRequest_DownloadImage>(
                        c =>
                        {
                            c.EntityID = entityID;
                            c.EntityType = (int)ImageEntityType.AniDB_Creator;
                            c.ForceDownload = true;
                        }
                    );
                    break;
                case ImageEntityType.AniDB_Character:
                    var characterpath = (entity as AniDB_Character)?.GetPosterPath();
                    if (string.IsNullOrEmpty(characterpath)) return;
                    try
                    {
                        if (File.Exists(characterpath)) File.Delete(characterpath);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError("Unable to delete {Characterpath} - {Message}", characterpath, e.Message);
                    }

                    cmd = _commandFactory.Create<CommandRequest_DownloadImage>(
                        c =>
                        {
                            c.EntityID = entityID;
                            c.EntityType = (int)ImageEntityType.AniDB_Character;
                            c.ForceDownload = true;
                        }
                    );
                    break;
                default:
                    return;
            }
            cmd.Save();
        }

        public override void GenerateCommandID()
        {
            CommandID = "CommandRequest_ValidateAllImages";
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            Priority = cq.Priority;
            CommandDetails = cq.CommandDetails;
            DateTimeUpdated = cq.DateTimeUpdated;

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

        public CommandRequest_ValidateAllImages(ILoggerFactory loggerFactory, ICommandRequestFactory commandFactory) : base(loggerFactory)
        {
            _commandFactory = commandFactory;
        }
    }
}