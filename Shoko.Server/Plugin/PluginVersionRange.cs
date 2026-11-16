using System;
using System.Text.RegularExpressions;
using Shoko.Abstractions.Utilities;

namespace Shoko.Server.Plugin;

/// <summary>
///   Parses and evaluates the version constraints used by plugin dependencies.
/// </summary>
/// <remarks>
///   Versions compare by semantic versioning, as
///   <see cref="SemverVersionComparer"/> orders them: a fourth part above
///   <c>0</c> is a prerelease of the three before it, so <c>0.1.0.3</c> is
///   <c>0.1.0-dev.3</c> and sorts below <c>0.1.0</c>. A range may give a
///   prerelease either way, <c>&gt;=0.1.0.3</c> or <c>&gt;=0.1.0-dev.3</c>.
/// </remarks>
internal static partial class PluginVersionRange
{
    private static readonly SemverVersionComparer _comparer = new();

    private enum RangeOperator
    {
        Exact,
        GreaterOrEqual,
        MinorRange,   // ^
        PatchRange,   // ~
    }

    private sealed record ParsedRange(RangeOperator Operator, Version Version);

    /// <summary>
    ///   A version written the semantic versioning way, with a <c>-dev.N</c>
    ///   prerelease after its three parts, as the server writes one.
    /// </summary>
    /// <returns>The expression.</returns>
    [GeneratedRegex(@"^(\d+)\.(\d+)\.(\d+)-dev\.(\d+)$")]
    private static partial Regex PrereleaseRegex();

    public static bool IsSatisfied(string versionRange, Version candidate)
        => TryParse(versionRange, out var range) && IsSatisfied(range, Normalize(candidate));

    /// <summary>
    ///   Whether a version range can be read at all.
    /// </summary>
    /// <param name="versionRange">The range.</param>
    /// <returns><c>true</c> when it can.</returns>
    public static bool IsValid(string versionRange)
        => TryParse(versionRange, out _);

    /// <summary>
    ///   Reads a version as a range writes it: two to four parts, or three
    ///   with a prerelease number, read as the fourth part.
    /// </summary>
    /// <param name="text">The version.</param>
    /// <param name="version">The version read, its missing parts <c>0</c>.</param>
    /// <returns><c>true</c> when it could be read.</returns>
    public static bool TryParseVersion(string text, out Version version)
    {
        version = null!;
        text = text.Trim();
        if (PrereleaseRegex().Match(text) is { Success: true } prerelease)
        {
            if (!int.TryParse(prerelease.Groups[1].Value, out var major) || !int.TryParse(prerelease.Groups[2].Value, out var minor) ||
                !int.TryParse(prerelease.Groups[3].Value, out var patch) || !int.TryParse(prerelease.Groups[4].Value, out var number) || number is 0)
                return false;

            version = new(major, minor, patch, number);
            return true;
        }

        if (!Version.TryParse(text, out var parsed))
            return false;

        version = Normalize(parsed);
        return true;
    }

    private static bool TryParse(string spec, out ParsedRange range)
    {
        range = null!;

        if (string.IsNullOrWhiteSpace(spec))
            return false;

        spec = spec.Trim();
        var (rangeOperator, versionText) = spec switch
        {
            _ when spec.StartsWith(">=") => (RangeOperator.GreaterOrEqual, spec[2..]),
            _ when spec.StartsWith('^') => (RangeOperator.MinorRange, spec[1..]),
            _ when spec.StartsWith('~') => (RangeOperator.PatchRange, spec[1..]),
            _ => (RangeOperator.Exact, spec),
        };
        if (!TryParseVersion(versionText, out var version))
            return false;

        range = new ParsedRange(rangeOperator, version);
        return true;
    }

    // Missing parts read as 0, so 1.2 and 1.2.0 are one version.
    private static Version Normalize(Version version)
        => new(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));

    private static bool IsSatisfied(ParsedRange range, Version candidate)
        => range.Operator switch
        {
            RangeOperator.Exact => _comparer.Compare(candidate, range.Version) is 0,
            RangeOperator.GreaterOrEqual => _comparer.Compare(candidate, range.Version) >= 0,
            RangeOperator.MinorRange => candidate.Major == range.Version.Major
                                        && _comparer.Compare(candidate, range.Version) >= 0,
            RangeOperator.PatchRange => candidate.Major == range.Version.Major
                                        && candidate.Minor == range.Version.Minor
                                        && _comparer.Compare(candidate, range.Version) >= 0,
            _ => false,
        };
}
