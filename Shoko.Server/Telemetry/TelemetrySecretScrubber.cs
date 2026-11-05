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

    /// <summary>
    /// Hides the values of the secrets in a text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text, with every secret's value replaced.</returns>
    public static string Scrub(string text)
        => GetRegex().Replace(text, Replacement);
}
