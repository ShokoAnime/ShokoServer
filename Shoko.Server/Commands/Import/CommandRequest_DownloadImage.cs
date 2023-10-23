using System.Net;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Properties;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.ImageDownload;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

namespace Shoko.Server.Commands;

[Command(CommandRequestType.ImageDownload)]
public class CommandRequest_DownloadImage : CommandRequestImplementation
{
    private const string FailedToDownloadNoID = "Image failed to download: Can\'t find valid {EntityType} with ID: {EntityID}";

    private const string FailedToDownloadNoImpl = "Image failed to download: No implementation found for {EntityType}";

    [XmlIgnore][JsonIgnore]
    private readonly IUDPConnectionHandler _handler;

    public virtual int EntityID { get; set; }

    public virtual int EntityType { get; set; }

    public virtual bool ForceDownload { get; set; }

    [XmlIgnore][JsonIgnore]
    public virtual ImageEntityType EntityTypeEnum
    {
        get => (ImageEntityType)EntityType;
        set => EntityType = (int)value;
    }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority2;

    public override QueueStateStruct PrettyDescription
    {
        get
        {
            string type = EntityTypeEnum switch
            {
                ImageEntityType.TvDB_Episode => Resources.Command_ValidateAllImages_TvDBEpisodes,
                ImageEntityType.TvDB_FanArt => Resources.Command_ValidateAllImages_TvDBFanarts,
                ImageEntityType.TvDB_Cover => Resources.Command_ValidateAllImages_TvDBPosters,
                ImageEntityType.TvDB_Banner => Resources.Command_ValidateAllImages_TvDBBanners,
                ImageEntityType.MovieDB_Poster => Resources.Command_ValidateAllImages_MovieDBPosters,
                ImageEntityType.MovieDB_FanArt => Resources.Command_ValidateAllImages_MovieDBFanarts,
                ImageEntityType.AniDB_Cover => Resources.Command_ValidateAllImages_AniDBPosters,
                ImageEntityType.AniDB_Character => Resources.Command_ValidateAllImages_AniDBCharacters,
                ImageEntityType.AniDB_Creator => Resources.Command_ValidateAllImages_AniDBSeiyuus,
                _ => string.Empty
            };

            return new QueueStateStruct
            {
                message = "Downloading Image {0}: {1}",
                queueState = QueueStateEnum.DownloadImage,
                extraParams = new[] { type, EntityID.ToString() }
            };
        }
    }

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_DownloadImage: {EntityID}", EntityID);
        ImageDownloadRequest req = null;

        switch (EntityTypeEnum)
        {
            case ImageEntityType.TvDB_Episode:
                var ep = RepoFactory.TvDB_Episode.GetByID(EntityID);
                if (string.IsNullOrEmpty(ep?.Filename))
                {
                    Logger.LogWarning(FailedToDownloadNoID, "TvDB episode", EntityID);
                    return;
                }

                req = new ImageDownloadRequest(ep, ForceDownload);
                break;

            case ImageEntityType.TvDB_FanArt:
                var fanart = RepoFactory.TvDB_ImageFanart.GetByID(EntityID);
                if (string.IsNullOrEmpty(fanart?.BannerPath))
                {
                    Logger.LogWarning(FailedToDownloadNoID, "TvDB fanart", EntityID);
                    RemoveImageRecord();
                    return;
                }

                req = new ImageDownloadRequest(fanart, ForceDownload);
                break;

            case ImageEntityType.TvDB_Cover:
                var poster = RepoFactory.TvDB_ImagePoster.GetByID(EntityID);
                if (string.IsNullOrEmpty(poster?.BannerPath))
                {
                    Logger.LogWarning(FailedToDownloadNoID, "TvDB poster", EntityID);
                    RemoveImageRecord();
                    return;
                }

                req = new ImageDownloadRequest(poster, ForceDownload);
                break;

            case ImageEntityType.TvDB_Banner:
                var wideBanner = RepoFactory.TvDB_ImageWideBanner.GetByID(EntityID);
                if (string.IsNullOrEmpty(wideBanner?.BannerPath))
                {
                    Logger.LogWarning(FailedToDownloadNoID, "TvDB banner", EntityID);
                    RemoveImageRecord();
                    return;
                }

                req = new ImageDownloadRequest(wideBanner, ForceDownload);
                break;

            case ImageEntityType.MovieDB_Poster:
                var moviePoster = RepoFactory.MovieDB_Poster.GetByID(EntityID);
                if (string.IsNullOrEmpty(moviePoster?.URL))
                {
                    Logger.LogWarning(FailedToDownloadNoID, "TMDB poster", EntityID);
                    RemoveImageRecord();
                    return;
                }

                req = new ImageDownloadRequest(moviePoster, ForceDownload);
                break;

            case ImageEntityType.MovieDB_FanArt:
                var movieFanart = RepoFactory.MovieDB_Fanart.GetByID(EntityID);
                if (string.IsNullOrEmpty(movieFanart?.URL))
                {
                    Logger.LogWarning(FailedToDownloadNoID, "TMDB fanart", EntityID);
                    RemoveImageRecord();
                    return;
                }

                req = new ImageDownloadRequest(movieFanart, ForceDownload);
                break;

            case ImageEntityType.AniDB_Cover:
                var anime = RepoFactory.AniDB_Anime.GetByAnimeID(EntityID);
                if (anime == null)
                {
                    Logger.LogWarning(FailedToDownloadNoID, "AniDB anime poster", EntityID);
                    return;
                }

                req = new ImageDownloadRequest(anime, ForceDownload, _handler.ImageServerUrl);
                break;

            case ImageEntityType.AniDB_Character:
                var chr = RepoFactory.AniDB_Character.GetByCharID(EntityID);
                if (chr == null)
                {
                    Logger.LogWarning(FailedToDownloadNoID, "AniDB character", EntityID);
                    return;
                }

                req = new ImageDownloadRequest(chr, ForceDownload, _handler.ImageServerUrl);
                break;

            case ImageEntityType.AniDB_Creator:
                var va = RepoFactory.AniDB_Seiyuu.GetBySeiyuuID(EntityID);
                if (va == null)
                {
                    Logger.LogWarning(FailedToDownloadNoID, "AniDB Seiyuu", EntityID);
                    return;
                }

                req = new ImageDownloadRequest(va, ForceDownload, _handler.ImageServerUrl);
                break;
        }

        if (req == null)
        {
            Logger.LogWarning(FailedToDownloadNoImpl, EntityTypeEnum.ToString());
            return;
        }

        try
        {
            // If this has any issues, it will throw an exception, so the catch below will handle it.
            var result = req.DownloadNow();
            switch (result)
            {
                case ImageDownloadResult.Success:
                    Logger.LogInformation("Image downloaded; {FilePath} from {DownloadUrl}", req.FilePath, req.DownloadUrl);
                    break;
                case ImageDownloadResult.Cached:
                    Logger.LogDebug("Image already in cache; {FilePath} from {DownloadUrl}", req.FilePath, req.DownloadUrl);
                    break;
                case ImageDownloadResult.Failure:
                    Logger.LogWarning("Image failed to download; {FilePath} from {DownloadUrl}", req.FilePath, req.DownloadUrl);
                    break;
                case ImageDownloadResult.RemovedResource:
                    Logger.LogWarning("Image failed to download and the local entry has been removed; {FilePath} from {DownloadUrl}", req.FilePath,
                        req.DownloadUrl);
                    break;
                case ImageDownloadResult.InvalidResource:
                    Logger.LogWarning("Image failed to download and the local entry could not be removed; {FilePath} from {DownloadUrl}", req.FilePath,
                        req.DownloadUrl);
                    break;
            }
        }
        catch (WebException e)
        {
            Logger.LogWarning("Error processing CommandRequest_DownloadImage: {Url} ({EntityID}) - {Message}",
                req.DownloadUrl,
                EntityID,
                e.Message);
            // Remove the record if the image doesn't exist or can't download
            RemoveImageRecord();
        }
    }

    private void RemoveImageRecord()
    {
        switch (EntityTypeEnum)
        {
            case ImageEntityType.TvDB_FanArt:
                var fanart = RepoFactory.TvDB_ImageFanart.GetByID(EntityID);
                if (fanart == null)
                    return;

                RepoFactory.TvDB_ImageFanart.Delete(fanart);
                break;

            case ImageEntityType.TvDB_Cover:
                var poster = RepoFactory.TvDB_ImagePoster.GetByID(EntityID);
                if (poster == null)
                    return;

                RepoFactory.TvDB_ImagePoster.Delete(poster);
                break;

            case ImageEntityType.TvDB_Banner:
                var wideBanner = RepoFactory.TvDB_ImageWideBanner.GetByID(EntityID);
                if (wideBanner == null)
                    return;

                RepoFactory.TvDB_ImageWideBanner.Delete(wideBanner);
                break;

            case ImageEntityType.MovieDB_Poster:
                var moviePoster = RepoFactory.MovieDB_Poster.GetByID(EntityID);
                if (moviePoster == null)
                    return;

                RepoFactory.MovieDB_Poster.Delete(moviePoster);
                break;

            case ImageEntityType.MovieDB_FanArt:
                var movieFanart = RepoFactory.MovieDB_Fanart.GetByID(EntityID);
                if (movieFanart == null)
                    return;

                RepoFactory.MovieDB_Fanart.Delete(movieFanart);
                break;
        }
    }

    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_DownloadImage_{EntityID}_{EntityType}";
    }

    protected override bool Load()
    {
        // read xml to get parameters
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        // populate the fields
        EntityID = int.Parse(docCreator.TryGetProperty("CommandRequest_DownloadImage", "EntityID"));
        EntityType = int.Parse(docCreator.TryGetProperty("CommandRequest_DownloadImage", "EntityType"));
        ForceDownload =
            bool.Parse(docCreator.TryGetProperty("CommandRequest_DownloadImage", "ForceDownload"));

        return true;
    }

    public CommandRequest_DownloadImage(ILoggerFactory loggerFactory, IUDPConnectionHandler handler) : base(
        loggerFactory)
    {
        _handler = handler;
    }

    protected CommandRequest_DownloadImage()
    {
    }
}
