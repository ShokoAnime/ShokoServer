using System;
using Shoko.Abstractions.Metadata.Image.CrossReferences;

namespace Shoko.Abstractions.Metadata.Events;

/// <summary>
/// Event arguments for image cross-reference-related events. This is dispatched when a
/// cross-reference between an image and an entity is added, updated, or removed.
/// </summary>
public class ImageCrossReferenceEventArgs : EventArgs
{
    /// <summary>
    /// The cross-reference that was added, updated, or removed.
    /// </summary>
    public required IImageCrossReference ImageCrossReference { get; init; }
}
