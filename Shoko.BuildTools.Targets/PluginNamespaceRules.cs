using System.Text.RegularExpressions;

namespace Shoko.BuildTools.Tasks;

/// <summary>
///   The rules a plugin's namespace follows: optional, kebab-case, 2 to 32
///   characters, and none of the reserved names. The server checks the same
///   rules when it loads the plugin.
/// </summary>
internal static class PluginNamespaceRules
{
    /// <summary>
    ///   The shortest namespace allowed.
    /// </summary>
    public const int MinLength = 2;

    /// <summary>
    ///   The longest namespace allowed.
    /// </summary>
    public const int MaxLength = 32;

    /// <summary>
    ///   The names no plugin may claim.
    /// </summary>
    public static readonly IReadOnlyList<string> Reserved = ["shoko", "core", "plugin"];

    private static readonly Regex _pattern = new("^[a-z][a-z0-9]*(-[a-z0-9]+)*$", RegexOptions.CultureInvariant);

    /// <summary>
    ///   Checks a namespace against the rules.
    /// </summary>
    /// <param name="value">The namespace.</param>
    /// <returns>Why it breaks them, or <c>null</c> when it follows them.</returns>
    public static string? Validate(string value)
    {
        if (value.Length is < MinLength or > MaxLength)
            return $"it must be {MinLength} to {MaxLength} characters long";

        if (!_pattern.IsMatch(value))
            return "it must be kebab-case: lowercase letters and digits, starting with a letter, with single hyphens between words";

        if (Reserved.Contains(value))
            return "it is reserved";

        return null;
    }
}
