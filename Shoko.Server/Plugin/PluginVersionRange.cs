using System;

namespace Shoko.Server.Plugin;

/// <summary>
///   Parses and evaluates the version constraints used by plugin dependencies.
/// </summary>
internal static class PluginVersionRange
{
    private enum RangeOperator
    {
        Exact,
        GreaterOrEqual,
        MinorRange,   // ^
        PatchRange,   // ~
    }

    private sealed record ParsedRange(RangeOperator Operator, Version Version);

    public static bool IsSatisfied(string versionRange, Version candidate)
        => TryParse(versionRange, out var range) && IsSatisfied(range, candidate);

    private static bool TryParse(string spec, out ParsedRange range)
    {
        range = null!;

        if (string.IsNullOrWhiteSpace(spec))
            return false;

        spec = spec.Trim();

        if (spec.StartsWith(">="))
        {
            if (Version.TryParse(spec[2..], out var v))
            {
                range = new ParsedRange(RangeOperator.GreaterOrEqual, v);
                return true;
            }
        }
        else if (spec.StartsWith('^'))
        {
            if (Version.TryParse(spec[1..], out var v))
            {
                range = new ParsedRange(RangeOperator.MinorRange, v);
                return true;
            }
        }
        else if (spec.StartsWith('~'))
        {
            if (Version.TryParse(spec[1..], out var v))
            {
                range = new ParsedRange(RangeOperator.PatchRange, v);
                return true;
            }
        }
        else
        {
            if (Version.TryParse(spec, out var v))
            {
                range = new ParsedRange(RangeOperator.Exact, v);
                return true;
            }
        }

        return false;
    }

    private static bool IsSatisfied(ParsedRange range, Version candidate)
        => range.Operator switch
        {
            RangeOperator.Exact => candidate == range.Version,
            RangeOperator.GreaterOrEqual => candidate >= range.Version,
            RangeOperator.MinorRange => candidate.Major == range.Version.Major
                                        && candidate >= range.Version,
            RangeOperator.PatchRange => candidate.Major == range.Version.Major
                                        && candidate.Minor == range.Version.Minor
                                        && candidate >= range.Version,
            _ => false,
        };
}
