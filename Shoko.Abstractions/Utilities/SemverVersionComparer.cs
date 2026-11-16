using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Utilities;

/// <summary>
///   Compares two versions using Semantic Versioning rules: a revision above
///   0 is a prerelease, <c>X.Y.Z-dev.N</c>, and sorts below the release
///   without one. A part left unset (<c>-1</c>) counts as 0, so <c>1.2</c>,
///   <c>1.2.0</c> and <c>1.2.0.0</c> are one version.
/// </summary>
public class SemverVersionComparer : IComparer<Version>
{
    /// <inheritdoc />
    public int Compare(Version? x, Version? y)
    {
        if (x is null)
            return y is null ? 0 : -1;
        if (y is null)
            return 1;

        var value = x.Major.CompareTo(y.Major);
        if (value != 0)
            return value;

        value = x.Minor.CompareTo(y.Minor);
        if (value != 0)
            return value;

        value = Math.Max(x.Build, 0).CompareTo(Math.Max(y.Build, 0));
        if (value != 0)
            return value;

        var (xPrerelease, yPrerelease) = (Math.Max(x.Revision, 0), Math.Max(y.Revision, 0));
        return (xPrerelease, yPrerelease) switch
        {
            (0, 0) => 0,
            (0, _) => 1,
            (_, 0) => -1,
            _ => xPrerelease.CompareTo(yPrerelease),
        };
    }
}
