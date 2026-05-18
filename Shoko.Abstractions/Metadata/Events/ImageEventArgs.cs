using System;
using Shoko.Abstractions.Metadata.Image;

namespace Shoko.Abstractions.Metadata.Events;

/// <summary>
/// Event arguments for image-related events. This is dispatched when an image is added,
/// updated, downloaded, or removed from the system.
/// </summary>
public class ImageEventArgs : EventArgs
{
    /// <summary>
    /// The image that was added, updated, downloaded, or removed.
    /// </summary>
    public required IImage Image { get; init; }
}
