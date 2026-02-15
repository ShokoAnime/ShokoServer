using System;

namespace Shoko.Abstractions.Config;

/// <summary>
/// Performs a redirect as part of the result of a configuration action.
/// </summary>
public class ConfigurationActionRedirect
{
    /// <summary>
    ///   The URL to redirect to. Can be an absolute path (with an optional
    ///   query) or an absolute URL. A value is deemed to be a a path if it does
    ///   not contain a leading http:// or https:// prefix. If it's a path, then
    ///   it will redirect within the web UI.
    /// </summary>
    public required string Location { get; init; }

    private bool? _openInNewTab;

    /// <summary>
    ///   Open the location in a new tab. Defaults to <c>false</c> if the
    ///   location is a path, and to <c>true</c> if it's an absolute URL.
    /// </summary>
    public bool OpenInNewTab
    {
        get => _openInNewTab ??= (Location.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || Location.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
        init => _openInNewTab = value;
    }
}
