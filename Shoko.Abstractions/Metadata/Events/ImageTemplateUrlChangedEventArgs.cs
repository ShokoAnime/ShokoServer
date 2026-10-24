using System;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Metadata.Events;

/// <summary>
///   Dispatched when the user's own template URL for an image source is set,
///   changed or cleared.
/// </summary>
public class ImageTemplateUrlChangedEventArgs : EventArgs
{
    /// <summary>
    ///   The image source.
    /// </summary>
    public required MetadataSource Source { get; init; }

    /// <summary>
    ///   The user's own template URL before the change, or <c>null</c> when
    ///   none was set.
    /// </summary>
    public required string? PreviousTemplateUrl { get; init; }

    /// <summary>
    ///   The user's own template URL after the change, or <c>null</c> when it
    ///   was cleared.
    /// </summary>
    public required string? TemplateUrl { get; init; }

    /// <summary>
    ///   The template URL in effect after the change: the user's own, else the
    ///   registered default, else <c>null</c>.
    /// </summary>
    public required string? TemplateUrlInEffect { get; init; }

    /// <summary>
    ///   The API token of whoever made the change, or <c>null</c> when the
    ///   system did it. Stamped when the event is raised.
    /// </summary>
    public ApiToken? Actor { get; init; }
}
