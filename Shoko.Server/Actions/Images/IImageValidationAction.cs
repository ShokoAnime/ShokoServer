using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.Options;

namespace Shoko.Server.Actions;

/// <summary>
///   The options every "Validate Images" action takes, at any level.
/// </summary>
public interface IImageValidationAction
{
    /// <summary>
    ///   Only the images from these sources, or every source when empty or
    ///   unset.
    /// </summary>
    List<MetadataSource>? ImageSources { get; set; }

    /// <summary>
    ///   Only the images of these types, or every type when empty or unset.
    /// </summary>
    List<ImageEntityType>? ImageTypes { get; set; }

    /// <summary>
    ///   Read each file to check it is a whole image, removing it when it is
    ///   not, rather than only checking it is there.
    /// </summary>
    bool ReadContents { get; set; }

    /// <summary>
    ///   Queue a download of every wanted image found missing or broken.
    /// </summary>
    bool Redownload { get; set; }
}

/// <summary>
///   Helpers for <see cref="IImageValidationAction"/>.
/// </summary>
public static class ImageValidationActionExtensions
{
    /// <summary>
    ///   Keeps the images of the sources and types an action picks.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="images">The images, each with its type for the entity it came from.</param>
    /// <returns>The images picked.</returns>
    public static IEnumerable<IImage> Pick(this IImageValidationAction action, IEnumerable<IImage> images)
    {
        var sources = action.ImageSources is { Count: > 0 } list ? list.ToHashSet() : null;
        var types = action.ImageTypes is { Count: > 0 } typeList ? typeList.ToHashSet() : null;
        return sources is null && types is null
            ? images
            : images.Where(image => (sources is null || sources.Contains(image.Source)) && (types is null || types.Contains(image.Type)));
    }

    /// <summary>
    ///   How an action validates its images.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <returns>The options.</returns>
    public static ImageValidationOptions GetOptions(this IImageValidationAction action)
        => new() { ReadContents = action.ReadContents, Redownload = action.Redownload };

    /// <summary>
    ///   The images of the characters and people credited on an entity.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <returns>The images.</returns>
    public static IEnumerable<IImage> CastAndCrewImagesOf(IWithCastAndCrew entity)
        => entity.Cast.SelectMany(cast => new IWithImages?[] { cast.Character, cast.Creator })
            .Concat(entity.Crew.Select(crew => (IWithImages?)crew.Creator))
            .OfType<IWithImages>()
            .SelectMany(person => person.GetImages());
}
