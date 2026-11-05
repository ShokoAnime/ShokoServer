using System;
using System.Text.RegularExpressions;

namespace Shoko.Server.Telemetry;

/// <summary>
/// Hides the values of well-known secrets in text about to leave the server. The last line of defense, for what a
/// plugin or a log message puts there: anything sending a secret should still keep it out in the first place.
/// </summary>
internal static partial class TelemetrySecretScrubber
{
    private const string Replacement = "Redacted";

    [GeneratedRegex(
        """
        (?<=
            (?:^|[?&\s;,"'(])
            (?:
                (?:pass|password|apikey|api_key|access_token|token|x-plex-token|s)=
                |
                (?:password|apikey|api_key|access_token|token|x-plex-token)["']?\s*:\s*["']?
            )
        )
        [^&\s;,"')]+
        """,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace
    )]
    private static partial Regex GetRegex();

    [GeneratedRegex(@"(?<=[?&;])(?<name>[^=&;#]+)=(?<value>[^&;#]*)", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant)]
    private static partial Regex QueryParameterRegex();

    // Wider than the list above, as a URL's parameters are the one place a name says what its value is, and a plugin may send anything in them.
    [GeneratedRegex("^s$|pass|pwd|secret|token|key|auth|sig|session|cred|cookie|user|login|email|code", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretParameterNameRegex();

    /// <summary>
    /// Hides the values of the secrets in a text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text, with every secret's value replaced.</returns>
    public static string Scrub(string text)
        => GetRegex().Replace(text, Replacement);

    /// <summary>
    /// Hides the values of the query parameters of a URL whose name suggests a secret or a person, keeping the rest to
    /// show what was asked for. A secret under a name that does not say so stays.
    /// </summary>
    /// <param name="url">The URL, or only its query.</param>
    /// <returns>The URL, with those values replaced.</returns>
    public static string ScrubUrl(string url)
        => QueryParameterRegex().Replace(
            url,
            parameter => SecretParameterNameRegex().IsMatch(Uri.UnescapeDataString(parameter.Groups["name"].Value))
                ? $"{parameter.Groups["name"].Value}={Replacement}"
                : parameter.Value
        );
}
