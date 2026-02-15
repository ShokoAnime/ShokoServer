
using System.Collections.Generic;
using System.Linq;
using Quartz;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Services;
using Shoko.Abstractions.User;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.TMDB;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Services.Abstraction;

public class AbstractImageManager(
    ISchedulerFactory schedulerFactory,
    AniDB_CharacterRepository anidbCharacterRepository,
    AniDB_CreatorRepository anidbCreatorRepository,
    AniDB_AnimeRepository anidbAnimeRepository,
    AniDB_Anime_Character_CreatorRepository anidbAnimeCharacterCreatorRepository,
    AniDB_Anime_PreferredImageRepository anidbPreferredImageRepository,
    AniDB_Episode_PreferredImageRepository anidbPreferredEpisodeImageRepository,
    TMDB_ImageRepository tmdbImageRepository,
    TMDB_Image_EntityRepository tmdbImageEntityRepository,
    AnimeSeriesRepository seriesRepository,
    CrossRef_AniDB_TMDB_MovieRepository xrefAnidbTmdbMovies,
    CrossRef_AniDB_TMDB_ShowRepository xrefAnidbTmdbShows,
    JMMUserRepository userRepository
) : IImageManager
{
    public IImage? GetImage(DataSource dataSource, ImageEntityType imageType, int imageId)
        => dataSource switch
        {
            DataSource.AniDB => imageType switch
            {
                ImageEntityType.Character => anidbCharacterRepository.GetByCharacterID(imageId)?.GetImageMetadata(),
                ImageEntityType.Creator => anidbCreatorRepository.GetByCreatorID(imageId)?.GetImageMetadata(),
                ImageEntityType.Poster => anidbAnimeRepository.GetByAnimeID(imageId)?.GetImageMetadata(),
                _ => null,
            },
            DataSource.TMDB => tmdbImageRepository.GetByID(imageId),
            DataSource.User => imageType switch
            {
                ImageEntityType.Art => userRepository.GetByID(imageId) is IUser { } user
                    ? user.PortraitImage
                    : null,
                _ => null
            },
            _ => null,
        };

    public IImage? GetRandomImage(DataSource dataSource, ImageEntityType imageType)
        => dataSource switch
        {
            DataSource.AniDB => imageType switch
            {
                ImageEntityType.Poster => anidbAnimeRepository.GetAll()
                    .Where(a => a?.PosterPath is not null && !a.GetAllTags().Contains("18 restricted"))
                    .GetRandomElement()?.GetImageMetadata(false),
                ImageEntityType.Character => anidbAnimeRepository.GetAll()
                    .Where(a => a is not null && !a.GetAllTags().Contains("18 restricted"))
                    .SelectMany(a => a.Characters).Select(a => a.Character)
                    .WhereNotNull()
                    .GetRandomElement()?.GetImageMetadata(),
                ImageEntityType.Creator => anidbAnimeRepository.GetAll()
                    .Where(a => a is not null && !a.GetAllTags().Contains("18 restricted"))
                    .SelectMany(a => a.Characters)
                    .SelectMany(a => anidbAnimeCharacterCreatorRepository.GetByCharacterID(a.CharacterID))
                    .Select(a => anidbCreatorRepository.GetByCreatorID(a.CreatorID))
                    .WhereNotNull()
                    .GetRandomElement()?.GetImageMetadata(),
                _ => null,
            },
            DataSource.TMDB => tmdbImageRepository.GetByType(imageType)
                .GetRandomElement(),
            _ => null,
        };

    public IShokoSeries? GetFirstSeriesForImage(IImage metadata)
    {
        switch (metadata.Source)
        {
            case DataSource.AniDB:
                return metadata.ImageType switch
                {
                    ImageEntityType.Poster => seriesRepository.GetByAnimeID(metadata.ID),
                    _ => null,
                };
            case DataSource.TMDB:
            {
                if (tmdbImageRepository.GetByID(metadata.ID) is not { } tmdbImage || tmdbImageEntityRepository.GetByRemoteFileName(tmdbImage.RemoteFileName) is not { Count: > 0 } entities)
                    return null;

                foreach (var entity in entities.OrderBy(e => e.TmdbEntityType).ThenBy(e => e.ReleasedAt).ThenBy(e => e.TMDB_Image_EntityID))
                {
                    switch (entity.TmdbEntityType)
                    {
                        case ForeignEntityType.Movie:
                            var movieXref = xrefAnidbTmdbMovies.GetByTmdbMovieID(entity.TmdbEntityID) is { Count: > 0 } movieXrefs ? movieXrefs[0] : null;
                            if (movieXref == null)
                                return null;
                            return seriesRepository.GetByAnimeID(movieXref.AnidbAnimeID);
                        case ForeignEntityType.Show:
                            var showXref = xrefAnidbTmdbShows.GetByTmdbShowID(entity.TmdbEntityID) is { Count: > 0 } showXrefs ? showXrefs[0] : null;
                            if (showXref == null)
                                return null;
                            return seriesRepository.GetByAnimeID(showXref.AnidbAnimeID);
                    }
                }

                return null;
            }

            default:
                return null;
        }
    }

    public bool SetEnabled(DataSource dataSource, ImageEntityType imageType, int imageId, bool value = true)
    {
        var animeIDs = new HashSet<int>();
        switch (dataSource)
        {
            case DataSource.AniDB:
                switch (imageType)
                {
                    case ImageEntityType.Poster:
                        var anime = anidbAnimeRepository.GetByAnimeID(imageId);
                        if (anime == null)
                            return false;

                        anime.ImageEnabled = value ? 1 : 0;
                        anidbAnimeRepository.Save(anime);
                        break;

                    default:
                        return false;
                }
                break;

            case DataSource.TMDB:
                var tmdbImage = tmdbImageRepository.GetByID(imageId);
                if (tmdbImage == null)
                    return false;

                tmdbImage.IsEnabled = value;
                tmdbImageRepository.Save(tmdbImage);

                var entities = tmdbImageEntityRepository.GetByRemoteFileName(tmdbImage.RemoteFileName);
                foreach (var entity in entities)
                    switch (entity.TmdbEntityType)
                    {
                        case ForeignEntityType.Show:
                            foreach (var xref in xrefAnidbTmdbShows.GetByTmdbShowID(entity.TmdbEntityID))
                                animeIDs.Add(xref.AnidbAnimeID);
                            break;
                        case ForeignEntityType.Movie:
                            foreach (var xref in xrefAnidbTmdbMovies.GetByTmdbMovieID(entity.TmdbEntityID))
                                animeIDs.Add(xref.AnidbAnimeID);
                            break;
                        default:
                            break;
                    }
                break;

            default:
                return false;
        }

        if (!value)
        {
            var animePreferredImages = anidbPreferredImageRepository.GetByImageSourceAndTypeAndID(dataSource, imageType, imageId);
            anidbPreferredImageRepository.Delete(animePreferredImages);
            var episodePreferredImages = anidbPreferredEpisodeImageRepository.GetByImageSourceAndTypeAndID(dataSource, imageType, imageId);
            anidbPreferredEpisodeImageRepository.Delete(episodePreferredImages);
        }

        var scheduler = schedulerFactory.GetScheduler().GetAwaiter().GetResult();
        foreach (var animeID in animeIDs)
            scheduler.StartJob<RefreshAnimeStatsJob>(a => a.AnimeID = animeID).GetAwaiter().GetResult();

        return true;
    }
}
