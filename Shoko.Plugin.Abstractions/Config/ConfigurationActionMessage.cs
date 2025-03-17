using Shoko.Plugin.Abstractions.Config.Enums;

namespace Shoko.Plugin.Abstractions.Config;

/// <summary>
/// A message to display to the user upon completing a configuration action, be
/// it successfully or not.
/// </summary>
public class ConfigurationActionResultMessage
{
    /// <summary>
    /// The title of the message.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// The message to display to the user.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// The color theme to use.
    /// </summary>
    public DisplayColorTheme Theme { get; init; }
}
