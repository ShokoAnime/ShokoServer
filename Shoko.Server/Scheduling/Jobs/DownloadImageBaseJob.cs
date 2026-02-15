using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Services;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Scheduling.Jobs;

[DatabaseRequired]
[NetworkRequired]
[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public abstract class DownloadImageBaseJob : BaseJob, IImageDownloadJob
{
    public string? ParentName { get; set; }

    public int ImageID { get; set; }

    public bool ForceDownload { get; set; }

    public abstract DataSource Source { get; }

    public virtual ImageEntityType ImageType { get; set; }

    public override string TypeName => $"Download {Source} Image";

    public override string Title => $"Downloading {Source} Image";

    public override Dictionary<string, object> Details => new()
    {
        { "Type", ImageType.ToString() },
        { "ImageID", ImageID },
    };

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job} for {Parent} -> Image Type: {ImageType} | ImageID: {EntityID}", GetType().Name, ParentName, ImageType, ImageID);

        var imageType = $"{Source} {ImageType.ToString().Replace("_", " ")}";
        var image = Utils.ServiceContainer.GetRequiredService<IImageManager>().GetImage(Source, ImageType, ImageID);
        if (image is null)
        {
            _logger.LogWarning("Image failed to download: Can\'t find valid {ImageType} with ID: {ImageID}", imageType, ImageID);
            return;
        }
        if (string.IsNullOrEmpty(image.LocalPath) || string.IsNullOrEmpty(image.RemoteURL))
        {
            _logger.LogWarning("Image failed to download: Can\'t find valid {ImageType} with ID: {ImageID}", imageType, ImageID);
            return;
        }

        try
        {
            var previouslyDownloaded = image.IsLocalAvailable;
            var result = await image.DownloadImage(ForceDownload);
            if (result && (ForceDownload || !previouslyDownloaded))
            {
                _logger.LogInformation("Image downloaded for {Parent}: {FilePath} from {DownloadUrl}", ParentName, image.LocalPath, image.RemoteURL);
                EmitEventForRelatedEntities(image, !previouslyDownloaded ? UpdateReason.ImageAdded : UpdateReason.ImageUpdated);
            }
            else if (result)
            {
                _logger.LogDebug("Image already in cache for {Parent}: {FilePath} from {DownloadUrl}", ParentName, image.LocalPath, image.RemoteURL);
                EmitEventForRelatedEntities(image, UpdateReason.ImageUpdated);
            }
            else
            {
                _logger.LogWarning("Image failed to download for {Parent}: {FilePath} from {DownloadUrl}", ParentName, image.LocalPath, image.RemoteURL);
            }
        }
        catch (Exception e)
        {
            switch (e)
            {
                case HttpRequestException hre when hre.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.ExpectationFailed:
                    if (RemoveRecord())
                        _logger.LogWarning("Image failed to download for {Parent} and the local entry has been removed: {FilePath} from {DownloadUrl}", ParentName, image.LocalPath, image.RemoteURL);
                    else
                        _logger.LogWarning("Image failed to download for {Parent} and the local entry could not be removed: {FilePath} from {DownloadUrl}", ParentName, image.LocalPath, image.RemoteURL);
                    break;

                default:
                    _logger.LogWarning("Error processing {Job} for {Parent}: {Url} - {Message}", GetType().Name, ParentName, image.RemoteURL, e.Message);
                    break;
            }
        }
    }

    private void EmitEventForRelatedEntities(IImage image, UpdateReason reason)
    {
        switch ((Source, ImageType))
        {
            case (DataSource.AniDB, ImageEntityType.Poster) when RepoFactory.AniDB_Anime.GetByAnimeID(ImageID) is { } anime:
                ShokoEventHandler.Instance.OnSeriesUpdated(anime, reason);
                break;

            case (DataSource.TMDB, _) when image is TMDB_Image tmdbImage:
                var linkedEntities = RepoFactory.TMDB_Image_Entity.GetByRemoteFileName(tmdbImage.RemoteFileName);
                foreach (var linkedEntity in linkedEntities)
                {
                    switch (linkedEntity.GetTmdbEntity())
                    {
                        case TMDB_Movie movie:
                            ShokoEventHandler.Instance.OnMovieUpdated(movie, reason);
                            break;

                        case TMDB_Show show:
                            ShokoEventHandler.Instance.OnSeriesUpdated(show, reason);
                            break;

                        case TMDB_Season season:
                        {
                            if (season.TmdbShow is not { } tmdbShow)
                                continue;

                            // Until we have proper season entities in the abstraction, just emit it for the series/show.
                            ShokoEventHandler.Instance.OnSeriesUpdated(tmdbShow, reason);
                            break;
                        }

                        case TMDB_Episode episode:
                        {
                            if (episode.TmdbShow is not { } tmdbShow)
                                continue;

                            ShokoEventHandler.Instance.OnEpisodeUpdated(tmdbShow, episode, reason);
                            break;
                        }
                    }
                }
                break;
        }
    }

    protected virtual bool RemoveRecord()
    {
        return false;
    }

    protected DownloadImageBaseJob() { }
}
