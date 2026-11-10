namespace Shoko.Abstractions.Metadata.Image.Options;

/// <summary>
///   How <see cref="Services.IImageManager.ValidateImages"/> validates a set of
///   images.
/// </summary>
public sealed record ImageValidationOptions
{
    /// <summary>
    ///   Read each file to check it is a whole image, removing it when it is
    ///   not, rather than only checking it is there.
    /// </summary>
    public bool ReadContents { get; init; } = true;

    /// <summary>
    ///   Queue a download of every wanted image found missing or broken.
    /// </summary>
    public bool Redownload { get; init; } = true;
}
