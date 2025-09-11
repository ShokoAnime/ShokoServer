using AbstractConfigurationActionRedirect = Shoko.Plugin.Abstractions.Config.ConfigurationActionRedirect;

namespace Shoko.Server.API.v3.Models.Configuration;

/// <summary>
/// Performs a redirect as part of the result of a configuration action.
/// </summary>
public class ConfigurationActionRedirect(AbstractConfigurationActionRedirect redirect)
{
    /// <summary>
    ///   The URL to redirect to. Can be an absolute path (with an optional
    ///   query) or an absolute URL. A value is deemed to be a a path if it does
    ///   not contain a leading http:// or https:// prefix. If it's a path, then
    ///   it will redirect within the web UI.
    /// </summary>
    public string Location { get; init; } = redirect.Location;

    /// <summary>
    ///   Open the location in a new tab. Defaults to <c>false</c> if the
    ///   location is a path, and to <c>true</c> if it's an absolute URL.
    /// </summary>
    public bool OpenInNewTab { get; init; } = redirect.OpenInNewTab;
}
