using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Extensions;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Server;
using Shoko.Server.Services;

using AbstractAnimeType = Shoko.Plugin.Abstractions.DataModels.AnimeType;
using AbstractEpisodeType = Shoko.Plugin.Abstractions.DataModels.EpisodeType;
using AnimeType = Shoko.Server.API.v3.Models.AniDB.AnimeType;
using EpisodeType = Shoko.Server.API.v3.Models.AniDB.EpisodeType;
using ImageEntityType = Shoko.Plugin.Abstractions.Enums.ImageEntityType;
using TitleLanguage = Shoko.Plugin.Abstractions.DataModels.TitleLanguage;

#nullable enable
namespace Shoko.Server.API.v3.Helpers;

public static class APIv3_Extensions
{
    public static CreatorRoleType ToCreatorRole(this TMDB_Movie_Crew crew)
        => ToCreatorRole(crew.Department, crew.Job);

    public static CreatorRoleType ToCreatorRole(this TMDB_Show_Crew crew)
        => ToCreatorRole(crew.Department, crew.Job);

    public static CreatorRoleType ToCreatorRole(this TMDB_Season_Crew crew)
        => ToCreatorRole(crew.Department, crew.Job);

    public static CreatorRoleType ToCreatorRole(this TMDB_Episode_Crew crew)
        => ToCreatorRole(crew.Department, crew.Job);

    private static CreatorRoleType ToCreatorRole(string department, string job)
        => department switch
        {
            // TODO: Implement this.
            _ => CreatorRoleType.Staff,
        };

    public static AnimeType ToV3Dto(this AbstractAnimeType animeType)
        => animeType switch
        {
            AbstractAnimeType.TVSeries => AnimeType.TV,
            AbstractAnimeType.Movie => AnimeType.Movie,
            AbstractAnimeType.OVA => AnimeType.OVA,
            AbstractAnimeType.TVSpecial => AnimeType.TVSpecial,
            AbstractAnimeType.Web => AnimeType.Web,
            AbstractAnimeType.Other => AnimeType.Other,
            _ => AnimeType.Unknown,
        };


    internal static EpisodeType ToV3Dto(this AbstractEpisodeType episodeType)
        => episodeType switch
        {
            AbstractEpisodeType.Episode => EpisodeType.Normal,
            AbstractEpisodeType.Special => EpisodeType.Special,
            AbstractEpisodeType.Parody => EpisodeType.Parody,
            AbstractEpisodeType.Credits => EpisodeType.ThemeSong,
            AbstractEpisodeType.Trailer => EpisodeType.Trailer,
            AbstractEpisodeType.Other => EpisodeType.Other,
            _ => EpisodeType.Unknown,
        };

    public static ComponentVersion ToDto(this WebUIUpdateService.ComponentVersion componentVersion)
        => new()
        {
            Commit = componentVersion.Commit,
            Description = componentVersion.Description,
            ReleaseChannel = componentVersion.ReleaseChannel,
            ReleaseDate = componentVersion.ReleaseDate,
            Tag = componentVersion.Tag,
            Version = componentVersion.Version,
        };

    public static IEnumerable<IImageMetadata> InLanguage(this IEnumerable<IImageMetadata> imageList, IReadOnlySet<TitleLanguage>? language = null)
        => language != null && language.Count > 0
            ? imageList.Where(title => language.Contains(title.Language))
            : imageList;

    public static Images ToDto(
        this IEnumerable<IImageMetadata> imageList,
        IReadOnlySet<TitleLanguage>? language = null,
        IImageMetadata? preferredPoster = null,
        IImageMetadata? preferredBackdrop = null,
        IImageMetadata? preferredThumbnail = null,
        bool includeDisabled = false,
        bool includeThumbnails = false,
        bool preferredImages = false,
        bool randomizeImages = false)
    {
        var images = new Images();
        if (includeThumbnails)
            images.Thumbnails ??= [];
        foreach (var image in imageList)
        {
            if (!includeDisabled && !image.IsEnabled)
                continue;

            if (language != null && !language.Contains(image.Language))
                continue;

            bool? preferredOverride = null;
            switch (image.ImageType)
            {
                case ImageEntityType.Poster:
                    if (image.IsEnabled && preferredPoster is not null && preferredPoster.Equals(image))
                        preferredOverride = true;
                    images.Posters.Add(new(image, preferredOverride));
                    break;
                case ImageEntityType.Banner:
                    images.Banners.Add(new(image));
                    break;
                case ImageEntityType.Backdrop:
                    if (image.IsEnabled && preferredBackdrop is not null && preferredBackdrop.Equals(image))
                        preferredOverride = true;
                    images.Backdrops.Add(new(image, preferredOverride));
                    break;
                case ImageEntityType.Logo:
                    images.Logos.Add(new(image));
                    break;
                case ImageEntityType.Thumbnail when includeThumbnails:
                    if (image.IsEnabled && preferredThumbnail is not null && preferredThumbnail.Equals(image))
                        preferredOverride = true;
                    images.Thumbnails!.Add(new(image, preferredOverride));
                    break;
                default:
                    break;
            }
        }

        if (preferredImages)
        {
            SetPreferredOrDefaultImage(images.Posters, randomizeImages);
            SetPreferredOrDefaultImage(images.Backdrops, randomizeImages);
            SetPreferredOrDefaultImage(images.Banners, randomizeImages);
            SetPreferredOrDefaultImage(images.Logos, randomizeImages);
            if (includeThumbnails)
                SetPreferredOrDefaultImage(images.Thumbnails!, randomizeImages);
        }

        return images;
    }

    private static void SetPreferredOrDefaultImage(List<Image> images, bool randomizeImages = false)
    {
        var poster = randomizeImages
            ? images.GetRandomElement()
            : images.FirstOrDefault(i => i.Preferred) ?? images.FirstOrDefault();
        images.Clear();
        if (poster is not null)
            images.Add(poster);
    }

    public static IReadOnlyList<Title> ToDto(this IEnumerable<TMDB_Title> titles, string? mainTitle = null, TMDB_Title? preferredTitle = null, IReadOnlySet<TitleLanguage>? language = null)
    {
        if (language != null && language.Count > 0)
            titles = titles.WhereInLanguages(language);

        return titles
            .Select(title => new Title(title, mainTitle, preferredTitle))
            .OrderByDescending(title => title.Preferred)
            .ThenByDescending(title => title.Default)
            .ThenBy(title => title.Language)
            .ToList();
    }

    public static IReadOnlyList<Overview> ToDto(this IEnumerable<TMDB_Overview> overviews, string? mainOverview = null, TMDB_Overview? preferredOverview = null, IReadOnlySet<TitleLanguage>? language = null)
    {
        if (language != null && language.Count > 0)
            overviews = overviews.WhereInLanguages(language);

        return overviews
            .Select(overview => new Overview(overview, mainOverview, preferredOverview))
            .OrderByDescending(overview => overview.Preferred)
            .ThenByDescending(overview => overview.Default)
            .ThenBy(overview => overview.Language)
            .ToList();
    }

    public static IReadOnlyList<ContentRating> ToDto(this IEnumerable<TMDB_ContentRating> contentRatings, IReadOnlySet<TitleLanguage>? language = null)
    {
        if (language != null && language.Count > 0)
            contentRatings = contentRatings.WhereInLanguages(language);

        return contentRatings
            .Select(contentRating => new ContentRating(contentRating))
            .ToList();
    }
}
