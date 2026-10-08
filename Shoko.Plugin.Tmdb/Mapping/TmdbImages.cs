using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;

using TmdbImageData = TMDbLib.Objects.General.ImageData;

namespace Shoko.Plugin.Tmdb.Mapping;

/// <summary>
///   Turns TMDB's images into the core's image candidates, and TMDB's
///   paths into the resource IDs the image template completes.
/// </summary>
public static class TmdbImages
{
    /// <summary>
    ///   The size the image template asks TMDB's image server for.
    /// </summary>
    public const string TemplateSize = "original";

    /// <summary>
    ///   The image template for an image server: the original size, with the
    ///   resource ID after it.
    /// </summary>
    /// <param name="imageServerUrl">The image server's base URL.</param>
    /// <returns>The template.</returns>
    public static string Template(string imageServerUrl)
        => $"{(imageServerUrl.EndsWith('/') ? imageServerUrl : imageServerUrl + "/")}{TemplateSize}/{{0}}";

    /// <summary>
    ///   The resource ID an image is stored under for TMDB's path: the path
    ///   without its leading slash, with an SVG asked for as a PNG.
    /// </summary>
    /// <param name="filePath">TMDB's path for the image.</param>
    /// <returns>The resource ID.</returns>
    public static string ResourceID(string filePath)
    {
        var path = filePath.StartsWith('/') ? filePath[1..] : filePath;
        return path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? path[..^4] + ".png" : path;
    }

    /// <summary>
    ///   The full address of an image, for a search result.
    /// </summary>
    /// <param name="imageServerUrl">The image server's base URL.</param>
    /// <param name="filePath">TMDB's path for the image, if any.</param>
    /// <returns>The URL, or <c>null</c> without a path.</returns>
    public static string? Url(string imageServerUrl, string? filePath)
        => string.IsNullOrEmpty(filePath) ? null : string.Format(Template(imageServerUrl), ResourceID(filePath));

    /// <summary>
    ///   The images TMDB names on an entry, by type, as the defaults stored
    ///   with the entry.
    /// </summary>
    /// <param name="paths">TMDB's path of each type's image, if any.</param>
    /// <returns>The resource IDs, by type, leaving out the types without a path.</returns>
    public static IReadOnlyDictionary<ImageEntityType, string> Defaults(params (ImageEntityType Type, string? FilePath)[] paths)
        => paths.Where(path => !string.IsNullOrEmpty(path.FilePath)).ToDictionary(path => path.Type, path => ResourceID(path.FilePath!));

    /// <summary>
    ///   TMDB's images of one type, as candidates in TMDB's order.
    /// </summary>
    /// <param name="images">The images.</param>
    /// <param name="imageType">Their type.</param>
    /// <returns>The candidates, leaving out the images without a path.</returns>
    public static IEnumerable<ImageCandidate> Candidates(IEnumerable<TmdbImageData>? images, ImageEntityType imageType)
        => (images ?? [])
            .Where(image => !string.IsNullOrEmpty(image.FilePath))
            .Select(image =>
            {
                var hasRating = image.VoteCount > 0 && image.VoteAverage >= 1;
                return new ImageCandidate
                {
                    ResourceID = ResourceID(image.FilePath!),
                    ImageType = imageType,
                    Width = image.Width > 0 ? image.Width : null,
                    Height = image.Height > 0 ? image.Height : null,
                    LanguageCode = TmdbTexts.Clean(image.Iso_639_1),
                    CountryCode = TmdbTexts.Clean(image.Iso_3166_1),
                    Rating = hasRating ? image.VoteAverage : null,
                    RatingVotes = hasRating ? image.VoteCount : null,
                };
            })
            .DistinctBy(candidate => candidate.ResourceID);

    /// <summary>
    ///   The one image TMDB names on an entry, such as a company's logo.
    /// </summary>
    /// <param name="filePath">TMDB's path for the image, if any.</param>
    /// <param name="imageType">Its type.</param>
    /// <returns>The candidate, or none.</returns>
    public static IEnumerable<ImageCandidate> Single(string? filePath, ImageEntityType imageType)
        => string.IsNullOrEmpty(filePath) ? [] : [new() { ResourceID = ResourceID(filePath), ImageType = imageType }];
}
