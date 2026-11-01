using System;
using Shoko.Abstractions.Core.Services;

namespace Shoko.Abstractions.Core;

/// <summary>
///   One message the server showed while starting, as listed by
///   <see cref="ISystemService.StartupMessages"/>.
/// </summary>
public sealed class StartupMessageEntry
{
    /// <summary>
    ///   The message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    ///   When the server showed it, in UTC.
    /// </summary>
    public required DateTime Time { get; init; }
}
