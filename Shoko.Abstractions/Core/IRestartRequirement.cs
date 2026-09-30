using System;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Abstractions.Core;

/// <summary>
///   A plugin's hold on a restart reason, returned by
///   <see cref="ISystemService.RequireRestart{TPlugin}(string)"/>. Dispose it once the reason
///   is gone, for instance when the setting behind it was changed back.
/// </summary>
/// <remarks>
///   The handle is the reason's identity: each call to
///   <see cref="ISystemService.RequireRestart{TPlugin}(string)"/> raises a reason of its own,
///   so keep the handle rather than raising again. Disposing twice is
///   harmless.
/// </remarks>
public interface IRestartRequirement : IDisposable
{
    /// <summary>
    ///   The reason this handle holds.
    /// </summary>
    RestartReason Reason { get; }

    /// <summary>
    ///   The plugin the reason was raised for.
    /// </summary>
    LocalPluginInfo Plugin { get; }

    /// <summary>
    ///   Whether the reason still stands, meaning the handle has not been
    ///   disposed.
    /// </summary>
    bool IsHeld { get; }
}
