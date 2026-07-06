using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Extensions;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Server;

namespace Shoko.Server.API.v3.Helpers;

public static class APIv3_Extensions
{
    public static CreatorRoleType ToCreatorRole(this TMDB_Crew crew)
        => ToCreatorRole(crew.Department, crew.Job);

    private static CreatorRoleType ToCreatorRole(string department, string job)
        => department switch
        {
            // TODO: Implement this.
            _ => CreatorRoleType.Staff,
        };

    public static List<SeasonWithYear> ToV3Dto(this IEnumerable<(int Year, YearlySeason Season)> seasons)
        => seasons
            .Select(season => new SeasonWithYear(season.Year, season.Season))
            .ToList();

    public static ComponentVersion ToDto(this ReleaseVersionInformation componentVersion)
        => new()
        {
            Commit = componentVersion.SourceRevision,
            Description = componentVersion.Description,
            ReleaseChannel = componentVersion.Channel,
            ReleaseDate = componentVersion.ReleasedAt,
            Tag = componentVersion.ReleaseTag,
            Version = componentVersion.Version.ToSemanticVersioningString(),
        };

    public static ComponentVersion ToDto(this WebReleaseVersionInformation componentVersion)
        => new()
        {
            Commit = componentVersion.SourceRevision,
            Description = componentVersion.Description,
            ReleaseChannel = componentVersion.Channel,
            ReleaseDate = componentVersion.ReleasedAt,
            Tag = componentVersion.ReleaseTag,
            Version = componentVersion.Version.ToSemanticVersioningString(),
            MinimumServerVersion = componentVersion.MinimumServerVersion?.ToSemanticVersioningString(),
        };

    public static IEnumerable<IImage> InLanguage(this IEnumerable<IImage> imageList, IReadOnlySet<TitleLanguage>? language = null)
        => language != null && language.Count > 0
            ? imageList.Where(title => language.Contains(title.Language))
            : imageList;

    public static Images ToDto(
        this IEnumerable<IImage> imageList,
        IReadOnlySet<TitleLanguage>? language = null,
        IImage? preferredPoster = null,
        IImage? preferredBackdrop = null,
        IImage? preferredLogo = null,
        bool preferredImages = false,
        bool randomizeImages = false,
        bool showLinkedIDs = false)
    {
        var images = new Images();
        foreach (var image in imageList)
        {
            if (language != null && !language.Contains(image.Language))
                continue;

            bool? preferredOverride = null;
            switch (image.Type)
            {
                case ImageEntityType.Primary:
                    if (preferredPoster is not null)
                        preferredOverride = image.IsEnabled && preferredPoster.Equals(image);
                    images.Posters.Add(new(image, showLinkedIDs, preferredOverride));
                    break;
                case ImageEntityType.Banner:
                    images.Banners.Add(new(image, showLinkedIDs));
                    break;
                case ImageEntityType.Backdrop:
                    if (preferredBackdrop is not null)
                        preferredOverride = image.IsEnabled && preferredBackdrop.Equals(image);
                    images.Backdrops.Add(new(image, showLinkedIDs, preferredOverride));
                    break;
                case ImageEntityType.Logo:
                    if (preferredLogo is not null)
                        preferredOverride = image.IsEnabled && preferredLogo.Equals(image);
                    images.Logos.Add(new(image, showLinkedIDs, preferredOverride));
                    break;
                case ImageEntityType.Disc:
                    images.Discs.Add(new(image, showLinkedIDs));
                    break;
            }
        }

        if (preferredImages)
        {
            SetPreferredOrDefaultImage(images.Posters, randomizeImages);
            SetPreferredOrDefaultImage(images.Backdrops, randomizeImages);
            SetPreferredOrDefaultImage(images.Banners, randomizeImages);
            SetPreferredOrDefaultImage(images.Logos, randomizeImages);
        }

        return images;
    }

    private static void SetPreferredOrDefaultImage(List<Image> images, bool randomizeImages = false)
    {
        var image = randomizeImages
            ? images.GetRandomElement()
            : images.FirstOrDefault(i => i.Preferred) ?? images.FirstOrDefault();
        images.Clear();
        if (image is not null)
            images.Add(image);
    }

    public static IReadOnlyList<Title> ToTitleDto<TTitle>(this IEnumerable<TTitle> titles, string? mainTitle = null, ITitle? preferredTitle = null, IReadOnlySet<TitleLanguage>? language = null) where TTitle : ITitle
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

    public static IReadOnlyList<Overview> ToOverviewDto<TText>(this IEnumerable<TText> overviews, string? mainOverview = null, IText? preferredOverview = null, IReadOnlySet<TitleLanguage>? language = null) where TText : IText
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
