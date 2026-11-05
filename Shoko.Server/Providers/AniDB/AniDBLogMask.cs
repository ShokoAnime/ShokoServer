using System.Text.RegularExpressions;

namespace Shoko.Server.Providers.AniDB;

/// <summary>
/// Hides the password and session of an AniDB request before it is logged. Both UDP commands and HTTP URLs carry them
/// as <c>key=value</c> pairs, with the session of a UDP command following either a space or an ampersand.
/// </summary>
internal static partial class AniDBLogMask
{
    [GeneratedRegex(@"(?<=\b(?:pass|s)=)[^&]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GetRegex();

    public static string Mask(string input)
        => GetRegex().Replace(input, "****");
}
