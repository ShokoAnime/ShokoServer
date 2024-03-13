using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Enums;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models.TMDB;

using TitleLanguage = Shoko.Plugin.Abstractions.DataModels.TitleLanguage;

#nullable enable
namespace Shoko.Server.API.v3.Helpers;

public static class APIv3_TMDB_Extensions
{
    public static Role.CreatorRoleType ToCreatorRole(this TMDB_Movie_Crew crew)
        => ToCreatorRole(crew.Department, crew.Job);

    public static Role.CreatorRoleType ToCreatorRole(this TMDB_Show_Crew crew)
        => ToCreatorRole(crew.Department, crew.Job);

    public static Role.CreatorRoleType ToCreatorRole(this TMDB_Season_Crew crew)
        => ToCreatorRole(crew.Department, crew.Job);

    public static Role.CreatorRoleType ToCreatorRole(this TMDB_Episode_Crew crew)
        => ToCreatorRole(crew.Department, crew.Job);

    private static Role.CreatorRoleType ToCreatorRole(string department, string job)
        => department switch
        {
            // TODO: Implement this.
            _ => Role.CreatorRoleType.Staff,
        };

    public static Images ToDto(this IEnumerable<TMDB_Image> imageList, TitleLanguage? language)
        => ToDto(imageList, language.HasValue ? new HashSet<TitleLanguage>() { language.Value } : null);

    public static IEnumerable<TMDB_Image> InLanguage(this IEnumerable<TMDB_Image> imageList, IReadOnlySet<TitleLanguage>? language = null)
        => language != null && language.Count > 0
            ? imageList.Where(title => language.Contains(title.Language))
            : imageList;

    public static Images ToDto(this IEnumerable<TMDB_Image> imageList, IReadOnlySet<TitleLanguage>? language = null)
    {
        var images = new Images();
        foreach (var image in imageList)
        {
            if (language != null && !language.Contains(image.Language))
                continue;

            var dto = new Image(image.TMDB_ImageID, image.ImageType, DataSourceType.TMDB, false, !image.IsEnabled);
            switch (image.ImageType)
            {
                case Server.ImageEntityType.Poster:
                    images.Posters.Add(dto);
                    break;
                case Server.ImageEntityType.Banner:
                    images.Banners.Add(dto);
                    break;
                case Server.ImageEntityType.Backdrop:
                    images.Backdrops.Add(dto);
                    break;
                case Server.ImageEntityType.Logo:
                    images.Logos.Add(dto);
                    break;
                default:
                    break;
            }
        }

        return images;
    }

    public static Image ToDto(this TMDB_Image image) =>
        new Image(image.TMDB_ImageID, image.ImageType, DataSourceType.TMDB, false, !image.IsEnabled);

    public static IReadOnlyList<Title> ToDto(this IEnumerable<TMDB_Title> titles, string? mainTitle = null, TMDB_Title? preferredTitle = null, IReadOnlySet<TitleLanguage>? language = null)
    {
        if (language != null && language.Count > 0)
            titles = titles.Where(title => language.Contains(title.Language));

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
            overviews = overviews.Where(contentRating => language.Contains(contentRating.Language));

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
            contentRatings = contentRatings.Where(contentRating => language.Contains(contentRating.Language));

        return contentRatings
            .Select(contentRating => new ContentRating(contentRating))
            .ToList();
    }
}
