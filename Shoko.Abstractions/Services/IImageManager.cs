
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Abstractions.Services;

/// <summary>
///   Responsible for managing images.
/// </summary>
public interface IImageManager
{
    /// <summary>
    ///   Get an image from an image source by ID.
    /// </summary>
    /// <param name="dataSource">
    ///   The image data source.
    /// </param>
    /// <param name="imageEntityType">
    ///   The image entity type.
    /// </param>
    /// <param name="imageID">
    ///   The image ID.
    /// </param>
    /// <returns>
    ///   The image if found, otherwise <c>null</c>.
    /// </returns>
    IImage? GetImage(DataSource dataSource, ImageEntityType imageEntityType, int imageID);

    /// <summary>
    ///   Get a random image from an image source and type.
    /// </summary>
    /// <param name="dataSource"></param>
    /// <param name="imageEntityType"></param>
    /// <returns></returns>
    IImage? GetRandomImage(DataSource dataSource, ImageEntityType imageEntityType);

    /// <summary>
    ///   Set the enabled state of an image.
    /// </summary>
    /// <param name="dataSource">
    ///   The image data source.
    /// </param>
    /// <param name="imageType">
    ///   The image entity type.
    /// </param>
    /// <param name="imageId">
    ///   The image ID.
    /// </param>
    /// <param name="value">
    ///   The value.
    /// </param>
    /// <returns>
    ///   <c>true</c> if the image enabled state was set, otherwise <c>false</c>.
    /// </returns>
    bool SetEnabled(DataSource dataSource, ImageEntityType imageType, int imageId, bool value = true);

    /// <summary>
    ///   Get the first available series for an image.
    /// </summary>
    /// <param name="image">
    ///   The image.
    /// </param>
    /// <returns>
    ///   The series if found, otherwise <c>null</c>.
    /// </returns>
    IShokoSeries? GetFirstSeriesForImage(IImage image);
}
