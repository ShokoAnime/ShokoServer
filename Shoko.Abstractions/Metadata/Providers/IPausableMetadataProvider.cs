using System;

namespace Shoko.Abstractions.Metadata.Providers;

/// <summary>
///   A provider that can say it cannot take work right now, and why, such as
///   while its source is rate limiting it.
/// </summary>
/// <remarks>
///   Optional. While you report being paused, the core holds back your
///   refresh, search and image jobs (and no other provider's). Raise
///   <see cref="PauseStatusChanged"/> on every change, since the core does not
///   poll. The status is shown through
///   <see cref="Services.IMetadataRefreshService.GetPauseStatus"/>, so say why
///   and, if you know, until when.
/// </remarks>
public interface IPausableMetadataProvider : IMetadataProvider
{
    /// <summary>
    ///   Whether you can take work right now, and if not, why and until when.
    ///   Return <see cref="MetadataProviderPauseStatus.NotPaused"/> while you
    ///   can.
    /// </summary>
    MetadataProviderPauseStatus PauseStatus { get; }

    /// <summary>
    ///   Raise this whenever <see cref="PauseStatus"/> changes.
    /// </summary>
    event EventHandler? PauseStatusChanged;
}
