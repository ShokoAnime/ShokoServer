using System.Collections.Generic;
using Shoko.Abstractions.Config.Enums;

namespace Shoko.Abstractions.Config;

/// <summary>
/// The result of a configuration action.
/// </summary>
public class ConfigurationActionResult
{
    private bool _showSaveMessage = false;

    /// <summary>
    /// Indicates that the default save message should be shown to the user.
    /// </summary>
    /// <remarks>
    /// If <see cref="Configuration"/> is set to a non-null value then this
    /// value will always be <c>false</c>.
    /// </remarks>
    public bool ShowSaveMessage
    {
        get => Configuration is null && _showSaveMessage;
        init => _showSaveMessage = value;
    }

    private bool _refreshConfiguration = false;

    /// <summary>
    /// Indicates that the configuration should be refreshed by the client
    /// because we've modified it.
    /// </summary>
    /// <remarks>
    /// If <see cref="Configuration"/> is set to a non-null value then this
    /// value will always be <c>false</c>.
    /// </remarks>
    public bool Refresh
    {
        get => Configuration is null && _refreshConfiguration;
        init => _refreshConfiguration = value;
    }

    /// <summary>
    ///   The configuration to partially or fully send back to the client
    ///   depending on what the result is for. When creating a new configuration
    ///   or loading an existing configuration it will be fully sent back,
    ///   otherwise only the changes since before the action was performed will
    ///   be sent back.
    /// </summary>
    public IConfiguration? Configuration { get; init; }

    /// <summary>
    /// Validation errors to show to the user after the action has been performed.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? ValidationErrors { get; init; }

    /// <summary>
    /// Indicates that existing validation errors should be kept.
    /// </summary>
    public bool KeepExistingValidationErrors { get; init; }

    /// <summary>
    /// Messages to show to the user after the action has been performed.
    /// </summary>
    public IReadOnlyList<ConfigurationActionResultMessage> Messages { get; init; } = [];

    /// <summary>
    /// The redirect to perform after the action has been performed.
    /// </summary>
    public ConfigurationActionRedirect? Redirect { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationActionResult"/> class.
    /// </summary>
    public ConfigurationActionResult() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationActionResult"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    public ConfigurationActionResult(IConfiguration? configuration) => Configuration = configuration;

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
