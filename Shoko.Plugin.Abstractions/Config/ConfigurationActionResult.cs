using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Config.Enums;

namespace Shoko.Plugin.Abstractions.Config;

/// <summary>
/// The result of a configuration action.
/// </summary>
public class ConfigurationActionResult
{
    /// <summary>
    /// Indicates that the default save message should be shown to the user.
    /// </summary>
    public bool ShowDefaultSaveMessage { get; init; } = false;

    /// <summary>
    /// Indicates that the configuration should be refreshed.
    /// </summary>
    public bool RefreshConfiguration { get; init; } = true;

    /// <summary>
    /// Any additional messages to show to the user.
    /// </summary>
    public IReadOnlyList<ConfigurationActionResultMessage> Messages { get; init; } = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationActionResult"/> class.
    /// </summary>
    public ConfigurationActionResult() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationActionResult"/> class.
    /// </summary>
    /// <param name="showDefaultSaveMessage">if set to <c>true</c> [show default save message].</param>
    public ConfigurationActionResult(bool showDefaultSaveMessage) => ShowDefaultSaveMessage = showDefaultSaveMessage;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationActionResult"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="theme">Optional. The theme.</param>
    public ConfigurationActionResult(string message, DisplayColorTheme theme = DisplayColorTheme.Default) => Messages = [new() { Message = message, Theme = theme }];

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationActionResult"/> class.
    /// </summary>
    /// <param name="messages">The messages.</param>
    public ConfigurationActionResult(IReadOnlyList<ConfigurationActionResultMessage> messages) => Messages = messages;
}
