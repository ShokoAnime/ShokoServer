using System;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Image.Exceptions;

/// <summary>
///   Thrown when attempting to add an image for a source that has no url
///   template set.
/// </summary>
public class MissingImageSourceTemplateUrlException : Exception
{
    /// <summary>
    ///    The image source missing an url template to use.
    /// </summary>
    public required DataSource ImageSource { get; init; }
}
