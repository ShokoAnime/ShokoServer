using System;
using System.Collections.Generic;
using System.Linq;

namespace Shoko.Server.Plugin;

/// <summary>
///   One plugin dependency, as carried by the <c>PackageDependencies</c>
///   assembly metadata and the <c>--dependencies</c> flag.
/// </summary>
internal readonly record struct PluginDependencyEntry(Guid PluginID, string VersionRange, bool IsOptional);

/// <summary>
///   Parses and formats plugin dependency lists:
///   <c>guid@range[:optional|:required][,guid@range[:optional|:required]]*</c>.
///   Whitespace around every separator is ignored, and so are empty entries.
///   The legacy <c>guid:range[:true]</c> form is still read, entry by entry.
///   Copied in <c>Shoko.BuildTools</c> and <c>Shoko.BuildTools.Targets</c>; keep them in sync.
/// </summary>
internal static class PluginDependencyList
{
    private const string Optional = "optional";

    private const string Required = "required";

    /// <summary>
    ///   Parse a dependency list. Entries that cannot be parsed are left out
    ///   and described in <paramref name="errors" />.
    /// </summary>
    public static List<PluginDependencyEntry> Parse(string? value, out List<string> errors)
    {
        errors = [];
        var dependencies = new List<PluginDependencyEntry>();
        if (string.IsNullOrWhiteSpace(value))
            return dependencies;

        foreach (var rawEntry in value.Split(','))
        {
            var entry = rawEntry.Trim();
            if (entry.Length == 0)
                continue;

            if (TryParseEntry(entry, out var dependency, out var error))
                dependencies.Add(dependency);
            else
                errors.Add(error);
        }

        return dependencies;
    }

    /// <summary>
    ///   Format a dependency list in its canonical form: no whitespace, and a
    ///   marker only on optional dependencies.
    /// </summary>
    public static string Format(IEnumerable<PluginDependencyEntry> dependencies)
        => string.Join(",", dependencies.Select(d => d.IsOptional
            ? $"{d.PluginID:D}@{d.VersionRange.Trim()}:{Optional}"
            : $"{d.PluginID:D}@{d.VersionRange.Trim()}"));

    /// <summary>
    ///   Check what parsing cannot: that every version range is one the
    ///   server understands, and that no plugin is listed twice.
    /// </summary>
    public static List<string> Validate(IEnumerable<PluginDependencyEntry> dependencies)
    {
        var errors = new List<string>();
        var seen = new HashSet<Guid>();
        foreach (var dependency in dependencies)
        {
            if (!seen.Add(dependency.PluginID))
                errors.Add($"Plugin '{dependency.PluginID:D}' is listed more than once.");

            if (!IsValidVersionRange(dependency.VersionRange))
                errors.Add(
                    $"'{dependency.VersionRange}' for plugin '{dependency.PluginID:D}' is not a version range; " +
                    "expected one of '>=1.0.0', '^1.0.0', '~1.0.0' or '1.0.0'.");
        }

        return errors;
    }

    /// <summary>
    ///   Whether <paramref name="range" /> is one of <c>&gt;=V</c>,
    ///   <c>^V</c>, <c>~V</c> or <c>V</c>, the forms the server evaluates.
    /// </summary>
    public static bool IsValidVersionRange(string range)
    {
        var spec = range.Trim();
        if (spec.StartsWith(">="))
            spec = spec[2..];
        else if (spec.StartsWith('^') || spec.StartsWith('~'))
            spec = spec[1..];

        return Version.TryParse(spec, out _);
    }

    private static bool TryParseEntry(string entry, out PluginDependencyEntry dependency, out string error)
    {
        dependency = default;

        // A GUID holds neither separator, so whichever comes first says which
        // form the entry is in.
        var separatorIndex = entry.IndexOfAny(['@', ':']);
        if (separatorIndex < 0)
        {
            error = $"'{entry}' has no version range; expected '<guid>@<range>'.";
            return false;
        }

        var isLegacy = entry[separatorIndex] == ':';
        var id = entry[..separatorIndex].Trim();
        if (!Guid.TryParse(id, out var pluginId))
        {
            error = $"'{entry}' does not start with a plugin ID; '{id}' is not a GUID.";
            return false;
        }

        var parts = entry[(separatorIndex + 1)..].Split(':');
        var range = parts[0].Trim();
        if (range.Length == 0)
        {
            error = $"'{entry}' has an empty version range.";
            return false;
        }

        if (parts.Length > 2)
        {
            error = $"'{entry}' has more than one ':' after the version range.";
            return false;
        }

        var isOptional = false;
        if (parts.Length == 2)
        {
            var marker = parts[1].Trim();
            if (isLegacy && bool.TryParse(marker, out var legacyOptional))
            {
                isOptional = legacyOptional;
            }
            else if (!isLegacy && string.Equals(marker, Optional, StringComparison.OrdinalIgnoreCase))
            {
                isOptional = true;
            }
            else if (isLegacy || !string.Equals(marker, Required, StringComparison.OrdinalIgnoreCase))
            {
                error = isLegacy
                    ? $"'{entry}' ends in ':{marker}'; the legacy form expects ':true' or ':false'."
                    : $"'{entry}' ends in ':{marker}'; expected ':{Optional}' or ':{Required}'.";
                return false;
            }
        }

        dependency = new PluginDependencyEntry(pluginId, range, isOptional);
        error = string.Empty;
        return true;
    }
}
