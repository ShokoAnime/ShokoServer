using System;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.Exceptions;

namespace Shoko.Server.Services;

/// <summary>
///   Keeps a file the server already has in hand as the held copy of an
///   image of a remote source, as if it had been downloaded, so the image
///   keeps its source and URL.
/// </summary>
public interface IImageFileStore
{
    /// <summary>
    ///   Writes a file as the held copy of a stored image, replacing any copy
    ///   it had.
    /// </summary>
    /// <param name="image">The stored image the file is of.</param>
    /// <param name="file">The image file, in a supported format.</param>
    /// <returns>The image as updated.</returns>
    /// <exception cref="ArgumentException">The image is not stored, or the file is not a valid image.</exception>
    /// <exception cref="UnsupportedImageTypeException">The file is not in a supported format.</exception>
    IImage StoreFile(IImage image, byte[] file);
}
