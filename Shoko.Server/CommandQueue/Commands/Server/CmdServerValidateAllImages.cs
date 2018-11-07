using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shoko.Commons.Properties;
using Shoko.Commons.Queue;
using Shoko.Commons.Utils;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

namespace Shoko.Server.CommandQueue.Commands.Server
{
    public class CmdServerValidateAllImages : BaseCommand, ICommand
    {
        public string ParallelTag { get; set; } = WorkTypes.Server.ToString();
        public int ParallelMax { get; set; } = 8;
        public int Priority { get; set; } = 3;

        public string Id => $"ValidateAllImages";

        public QueueStateStruct PrettyDescription { get; set; } = new QueueStateStruct
        {
            QueueState = QueueStateEnum.ValidateAllImages,
            ExtraParams = new[] {string.Empty}
        };

        public WorkTypes WorkType => WorkTypes.Server;

        // ReSharper disable once UnusedParameter.Local
        public CmdServerValidateAllImages(string str) 
        {
        }
        public CmdServerValidateAllImages()
        {
        }
        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info("Processing CommandRequest_ValidateAllImages");
            try
            {
                QueueStateStruct queueState = PrettyDescription;
                InitProgress(progress);
                List<ICommand> cmds = new List<ICommand>();
                queueState.ExtraParams = new[] {Resources.Command_ValidateAllImages_TvDBEpisodes};
                int count = 0;
                logger.Info("Scanning TvDB Episode thumbs for corrupted images");
                var episodes = Repo.Instance.TvDB_Episode.GetAll().Where(fanart =>
                        !string.IsNullOrEmpty(fanart.GetFullImagePath()) &&
                        !Misc.IsImageValid(fanart.GetFullImagePath())).ToList();
                UpdateAndReportProgress(progress, 10);

                logger.Info($"Found {episodes.Count} corrupted TvDB Episode {(episodes.Count == 1 ? "thumb" : "thumbs")}");
                foreach (var fanart in episodes)
                {
                    logger.Trace($"Corrupt image found! Attempting Redownload: {fanart.GetFullImagePath()}");
                    RemoveImageAndQueueRedownload(ImageEntityType.TvDB_Episode, fanart.TvDB_EpisodeID, fanart,cmds);
                    count++;
                    if (count % 10 == 1 || count==episodes.Count)
                    {
                        logger.Info($"Deleting and queueing for redownload {count}/{episodes.Count}");
                        queueState.ExtraParams = new[]
                            {$"{Resources.Command_ValidateAllImages_TvDBEpisodes} - {count}/{episodes.Count}"};
                        UpdateAndReportProgress(progress, 10+(count*10/episodes.Count));
                    }
                }

                if (ServerSettings.Instance.TvDB.AutoFanart)
                {
                    count = 0;
                    queueState.ExtraParams = new[] {Resources.Command_ValidateAllImages_TvDBFanarts};
                    UpdateAndReportProgress(progress, 20);
                    logger.Info("Scanning TvDB Fanarts for corrupted images");
                    var fanarts = Repo.Instance.TvDB_ImageFanart.GetAll().Where(fanart =>
                        !string.IsNullOrEmpty(fanart.GetFullImagePath()) &&
                        !Misc.IsImageValid(fanart.GetFullImagePath())).ToList();

                    logger.Info($"Found {fanarts.Count} corrupted TvDB {(fanarts.Count == 1 ? "Fanart" : "Fanarts")}");
                    foreach (var fanart in fanarts)
                    {
                        logger.Trace($"Corrupt image found! Attempting Redownload: {fanart.GetFullImagePath()}");
                        RemoveImageAndQueueRedownload(ImageEntityType.TvDB_FanArt, fanart.TvDB_ImageFanartID, fanart,cmds);
                        count++;
                        if (count % 10 == 1 || count == fanarts.Count)
                        {
                            logger.Info($"Deleting and queueing for redownload {count}/{fanarts.Count}");
                            queueState.ExtraParams = new[]
                                {$"{Resources.Command_ValidateAllImages_TvDBFanarts} - {count}/{fanarts.Count}"};
                            UpdateAndReportProgress(progress, 20 + (count * 10 / fanarts.Count));
                        }
                    }
                }

                if (ServerSettings.Instance.TvDB.AutoPosters)
                {
                    count = 0;
                    queueState.ExtraParams = new[] {Resources.Command_ValidateAllImages_TvDBPosters};
                    UpdateAndReportProgress(progress, 30);
                    logger.Info("Scanning TvDB Posters for corrupted images");
                    var fanarts = Repo.Instance.TvDB_ImagePoster.GetAll().Where(fanart =>
                            !string.IsNullOrEmpty(fanart.GetFullImagePath()) &&
                            !Misc.IsImageValid(fanart.GetFullImagePath())).ToList();

                    logger.Info($"Found {fanarts.Count} corrupted TvDB {(fanarts.Count == 1 ? "Poster" : "Posters")}");
                    foreach (var fanart in fanarts)
                    {
                        logger.Trace($"Corrupt image found! Attempting Redownload: {fanart.GetFullImagePath()}");
                        RemoveImageAndQueueRedownload(ImageEntityType.TvDB_Cover, fanart.TvDB_ImagePosterID, fanart,cmds);
                        count++;
                        if (count % 10 == 1 || count == fanarts.Count)
                        {
                            logger.Info($"Deleting and queueing for redownload {count}/{fanarts.Count}");
                            queueState.ExtraParams = new[]
                                {$"{Resources.Command_ValidateAllImages_TvDBPosters} - {count}/{fanarts.Count}"};
                            UpdateAndReportProgress(progress, 30 + (count * 10 / fanarts.Count));
                        }
                    }
                }

                if (ServerSettings.Instance.TvDB.AutoWideBanners)
                {
                    count = 0;
                    logger.Info("Scanning TvDB Banners for corrupted images");
                    queueState.ExtraParams = new[] {Resources.Command_ValidateAllImages_TvDBBanners};
                    UpdateAndReportProgress(progress, 40);
                    var fanarts = Repo.Instance.TvDB_ImageWideBanner.GetAll().Where(fanart =>
                        !string.IsNullOrEmpty(fanart.GetFullImagePath()) &&
                        !Misc.IsImageValid(fanart.GetFullImagePath())).ToList();

                    logger.Info($"Found {fanarts.Count} corrupted TvDB {(fanarts.Count == 1 ? "Banner" : "Banners")}");
                    foreach (var fanart in fanarts)
                    {
                        logger.Trace($"Corrupt image found! Attempting Redownload: {fanart.GetFullImagePath()}");
                        RemoveImageAndQueueRedownload(ImageEntityType.TvDB_Banner, fanart.TvDB_ImageWideBannerID, fanart,cmds);
                        count++;
                        if (count % 10 == 1 || count == fanarts.Count)
                        {
                            logger.Info($"Deleting and queueing for redownload {count}/{fanarts.Count}");
                            queueState.ExtraParams = new[]
                                {$"{Resources.Command_ValidateAllImages_TvDBBanners} - {count}/{fanarts.Count}"};
                            UpdateAndReportProgress(progress, 40 + (count * 10 / fanarts.Count));
                        }
                    }
                }

                if (ServerSettings.Instance.MovieDb.AutoPosters)
                {
                    queueState.ExtraParams = new[] {Resources.Command_ValidateAllImages_MovieDBPosters};
                    UpdateAndReportProgress(progress, 50);
                    count = 0;
                    logger.Info("Scanning MovieDB Posters for corrupted images");
                    var fanarts = Repo.Instance.MovieDB_Poster.GetAll().Where(fanart =>
                        !string.IsNullOrEmpty(fanart.GetFullImagePath()) &&
                        !Misc.IsImageValid(fanart.GetFullImagePath())).ToList();

                    logger.Info($"Found {fanarts.Count} corrupted MovieDB {(fanarts.Count == 1 ? "Poster" : "Posters")}");
                    foreach (var fanart in fanarts)
                    {
                        logger.Trace($"Corrupt image found! Attempting Redownload: {fanart.GetFullImagePath()}");
                        RemoveImageAndQueueRedownload(ImageEntityType.MovieDB_Poster, fanart.MovieDB_PosterID, fanart,cmds);
                        count++;
                        if (count % 10 == 1 || count == fanarts.Count)
                        {
                            logger.Info($"Deleting and queueing for redownload {count}/{fanarts.Count}");
                            queueState.ExtraParams = new[]
                                {$"{Resources.Command_ValidateAllImages_MovieDBPosters} - {count}/{fanarts.Count}"};
                            UpdateAndReportProgress(progress, 50 + (count * 10 / fanarts.Count));
                        }
                    }
                }

                if (ServerSettings.Instance.MovieDb.AutoFanart)
                {
                    queueState.ExtraParams = new[] {Resources.Command_ValidateAllImages_MovieDBFanarts};
                    UpdateAndReportProgress(progress, 60);
                    count = 0;
                    logger.Info("Scanning MovieDB Fanarts for corrupted images");
                    var fanarts = Repo.Instance.MovieDB_Fanart.GetAll().Where(fanart =>
                            !string.IsNullOrEmpty(fanart.GetFullImagePath()) &&
                            !Misc.IsImageValid(fanart.GetFullImagePath())).ToList();
                    logger.Info($"Found {fanarts.Count} corrupted MovieDB {(fanarts.Count == 1 ? "Fanart" : "Fanarts")}");
                    foreach (var fanart in fanarts)
                    {
                        logger.Trace($"Corrupt image found! Attempting Redownload: {fanart.GetFullImagePath()}");
                        RemoveImageAndQueueRedownload(ImageEntityType.MovieDB_FanArt, fanart.MovieDB_FanartID, fanart,cmds);
                        count++;
                        if (count % 10 == 1 || count == fanarts.Count)
                        {
                            logger.Info($"Deleting and queueing for redownload {count}/{fanarts.Count}");
                            queueState.ExtraParams = new[]
                                {$"{Resources.Command_ValidateAllImages_MovieDBFanarts} - {count}/{fanarts.Count}"};
                            UpdateAndReportProgress(progress, 60 + (count * 10 / fanarts.Count));
                        }
                    }
                }

                queueState.ExtraParams = new[] {Resources.Command_ValidateAllImages_AniDBPosters};
                UpdateAndReportProgress(progress, 70);
                count = 0;
                logger.Info("Scanning AniDB Posters for corrupted images");
                var posters = Repo.Instance.AniDB_Anime.GetAll().Where(fanart =>
                        !string.IsNullOrEmpty(fanart.PosterPath) && !Misc.IsImageValid(fanart.PosterPath)).ToList();
                logger.Info($"Found {posters.Count} corrupted AniDB {(posters.Count == 1 ? "Poster" : "Posters")}");
                foreach (var fanart in posters)
                {
                    logger.Trace($"Corrupt image found! Attempting Redownload: {fanart.PosterPath}");
                    RemoveImageAndQueueRedownload(ImageEntityType.AniDB_Cover, fanart.AnimeID, fanart,cmds);
                    count++;
                    if (count % 10 == 1 || count == posters.Count)
                    {
                        logger.Info($"Deleting and queueing for redownload {count}/{posters.Count}");
                        queueState.ExtraParams = new[]
                            {$"{Resources.Command_ValidateAllImages_AniDBPosters} - {count}/{posters.Count}"};
                        UpdateAndReportProgress(progress, 70 + (count * 10 / posters.Count));
                    }
                }

                if (ServerSettings.Instance.AniDb.DownloadCharacters)
                {
                    queueState.ExtraParams = new[] {Resources.Command_ValidateAllImages_AniDBCharacters};
                    UpdateAndReportProgress(progress, 80);
                    count = 0;
                    logger.Info("Scanning AniDB Characters for corrupted images");
                    var fanarts = Repo.Instance.AniDB_Character.GetAll().Where(fanart =>
                            !string.IsNullOrEmpty(fanart.GetPosterPath()) && !Misc.IsImageValid(fanart.GetPosterPath()))
                        .ToList();
                    logger.Info($"Found {fanarts.Count} corrupted AniDB Character {(fanarts.Count == 1 ? "image" : "images")}");
                    foreach (var fanart in fanarts)
                    {
                        logger.Trace($"Corrupt image found! Attempting Redownload: {fanart.GetPosterPath()}");
                        RemoveImageAndQueueRedownload(ImageEntityType.AniDB_Character, fanart.AniDB_CharacterID, fanart,cmds);
                        count++;
                        if (count % 10 == 1 || count == fanarts.Count)
                        {
                            logger.Info($"Deleting and queueing for redownload {count}/{fanarts.Count}");
                            queueState.ExtraParams = new[]
                                {$"{Resources.Command_ValidateAllImages_AniDBCharacters} - {count}/{fanarts.Count}"};
                            UpdateAndReportProgress(progress, 80 + (count * 10 / fanarts.Count));
                        }
                    }
                }

                if (ServerSettings.Instance.AniDb.DownloadCreators)
                {
                    queueState.ExtraParams = new[] {Resources.Command_ValidateAllImages_AniDBSeiyuus};
                    UpdateAndReportProgress(progress, 90);
                    count = 0;
                    logger.Info("Scanning AniDB Seiyuus for corrupted images");
                    var fanarts = Repo.Instance.AniDB_Seiyuu.GetAll().Where(fanart =>
                        !string.IsNullOrEmpty(fanart.GetPosterPath()) && !Misc.IsImageValid(fanart.GetPosterPath())).ToList();
                    logger.Info($"Found {fanarts.Count} corrupted AniDB Seiyuu {(fanarts.Count == 1 ? "image" : "images")}");
                    foreach (var fanart in fanarts)
                    {
                        logger.Trace($"Corrupt image found! Attempting Redownload: {fanart.GetPosterPath()}");
                        RemoveImageAndQueueRedownload(ImageEntityType.AniDB_Creator, fanart.SeiyuuID, fanart,cmds);
                        count++;
                        if (count % 10 == 1 || count == fanarts.Count)
                        {
                            logger.Info($"Deleting and queueing for redownload {count}/{fanarts.Count}");
                            queueState.ExtraParams = new[]
                                {$"{Resources.Command_ValidateAllImages_AniDBSeiyuus} - {count}/{fanarts.Count}"};
                            UpdateAndReportProgress(progress, 90 + (count * 10 / fanarts.Count));
                        }
                    }
                }
                if (cmds.Count>0)
                    Queue.Instance.AddRange(cmds);
                ReportFinishAndGetResult(progress);

            }
            catch (Exception ex)
            {
                ReportErrorAndGetResult(progress, $"Error processing ServerValidateAllImages: {ex.Message}", ex);
            }
        }

        private void RemoveImageAndQueueRedownload(ImageEntityType EntityTypeEnum, int EntityID, object Entity, List<ICommand> cmds)
        {
            switch (EntityTypeEnum)
            {
                case ImageEntityType.TvDB_Episode:
                    TvDB_Episode episode = Entity as TvDB_Episode;
                    if (episode == null) return;
                    try
                    {
                        if (File.Exists(episode.GetFullImagePath())) File.Delete(episode.GetFullImagePath());
                    }
                    catch (Exception e)
                    {
                        logger.Error($"Unable to delete {episode.GetFullImagePath()} - {e.Message}");
                    }
                    cmds.Add(new Image.CmdImageDownload(EntityID, ImageEntityType.TvDB_Episode, true));
                    break;

                case ImageEntityType.TvDB_FanArt:
                    TvDB_ImageFanart fanart = Entity as TvDB_ImageFanart;
                    if (fanart == null) return;
                    try
                    {
                        if (File.Exists(fanart.GetFullImagePath())) File.Delete(fanart.GetFullImagePath());
                    }
                    catch (Exception e)
                    {
                        logger.Error($"Unable to delete {fanart.GetFullImagePath()} - {e.Message}");
                    }

                    cmds.Add(new Image.CmdImageDownload(EntityID, ImageEntityType.TvDB_FanArt, true));
                    break;

                case ImageEntityType.TvDB_Cover:
                    TvDB_ImagePoster poster = Entity as TvDB_ImagePoster;
                    if (poster == null) return;
                    try
                    {
                        if (File.Exists(poster.GetFullImagePath())) File.Delete(poster.GetFullImagePath());
                    }
                    catch (Exception e)
                    {
                        logger.Error($"Unable to delete {poster.GetFullImagePath()} - {e.Message}");
                    }
                    cmds.Add(new Image.CmdImageDownload(EntityID, ImageEntityType.TvDB_Cover, true));
                    break;

                case ImageEntityType.TvDB_Banner:
                    TvDB_ImageWideBanner wideBanner = Entity as TvDB_ImageWideBanner;
                    if (wideBanner == null) return;
                    try
                    {
                        if (File.Exists(wideBanner.GetFullImagePath())) File.Delete(wideBanner.GetFullImagePath());
                    }
                    catch (Exception e)
                    {
                        logger.Error($"Unable to delete {wideBanner.GetFullImagePath()} - {e.Message}");
                    }

                    cmds.Add(new Image.CmdImageDownload(EntityID, ImageEntityType.TvDB_Banner, true));
                    break;

                case ImageEntityType.MovieDB_Poster:
                    MovieDB_Poster moviePoster = Entity as MovieDB_Poster;
                    if (moviePoster == null) return;
                    try
                    {
                        if (File.Exists(moviePoster.GetFullImagePath())) File.Delete(moviePoster.GetFullImagePath());
                    }
                    catch (Exception e)
                    {
                        logger.Error($"Unable to delete {moviePoster.GetFullImagePath()} - {e.Message}");
                    }

                    cmds.Add(new Image.CmdImageDownload(EntityID, ImageEntityType.MovieDB_Poster, true));
                    break;

                case ImageEntityType.MovieDB_FanArt:
                    MovieDB_Fanart movieFanart = Entity as MovieDB_Fanart;
                    if (movieFanart == null) return;
                    try
                    {
                        if (File.Exists(movieFanart.GetFullImagePath())) File.Delete(movieFanart.GetFullImagePath());
                    }
                    catch (Exception e)
                    {
                        logger.Error($"Unable to delete {movieFanart.GetFullImagePath()} - {e.Message}");
                    }

                    cmds.Add(new Image.CmdImageDownload(EntityID, ImageEntityType.MovieDB_FanArt, true));
                    break;
                case ImageEntityType.AniDB_Cover:
                    string coverpath = (Entity as SVR_AniDB_Anime)?.PosterPath;
                    if (string.IsNullOrEmpty(coverpath)) return;
                    try
                    {
                        if (File.Exists(coverpath)) File.Delete(coverpath);
                    }
                    catch (Exception e)
                    {
                        logger.Error($"Unable to delete {coverpath} - {e.Message}");
                    }

                    cmds.Add(new Image.CmdImageDownload(EntityID, ImageEntityType.AniDB_Cover, true));
                    break;
                case ImageEntityType.AniDB_Creator:
                    string creatorpath = (Entity as AniDB_Seiyuu)?.GetPosterPath();
                    if (string.IsNullOrEmpty(creatorpath)) return;
                    try
                    {
                        if (File.Exists(creatorpath)) File.Delete(creatorpath);
                    }
                    catch (Exception e)
                    {
                        logger.Error($"Unable to delete {creatorpath} - {e.Message}");
                    }

                    cmds.Add(new Image.CmdImageDownload(EntityID, ImageEntityType.AniDB_Creator, true));
                    break;
                case ImageEntityType.AniDB_Character:
                    string characterpath = (Entity as AniDB_Character)?.GetPosterPath();
                    if (string.IsNullOrEmpty(characterpath)) return;
                    try
                    {
                        if (File.Exists(characterpath)) File.Delete(characterpath);
                    }
                    catch (Exception e)
                    {
                        logger.Error($"Unable to delete {characterpath} - {e.Message}");
                    }

                    cmds.Add(new Image.CmdImageDownload(EntityID, ImageEntityType.AniDB_Character, true));
                    break;
                default:
                    return;
            }
        }
    }
}