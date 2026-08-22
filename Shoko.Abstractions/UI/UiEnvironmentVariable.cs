namespace Shoko.Abstractions.UI;

/// <summary>
/// Describes the environment variable backing an element.
/// </summary>
public class UiEnvironmentVariable
{
    /// <summary>
    /// The name of the environment variable.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Whether the user may override the loaded value from the client.
    /// </summary>
    public bool AllowOverride { get; init; }
}
