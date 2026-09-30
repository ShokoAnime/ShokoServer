using System;
using System.Collections.Generic;

namespace Shoko.Server.Services;

/// <summary>
///   Which metadata providers report that they cannot take work right now.
/// </summary>
internal interface IMetadataProviderPauseState
{
    /// <summary>
    ///   Raised when a provider reports that it was paused or resumed, and once
    ///   the providers are registered.
    /// </summary>
    event EventHandler? PausedProvidersChanged;

    /// <summary>
    ///   The types of the registered providers that report they cannot take
    ///   work right now.
    /// </summary>
    /// <returns>The provider types.</returns>
    IReadOnlyList<Type> GetPausedProviderTypes();
}
