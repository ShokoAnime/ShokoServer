using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Utilities;

/// <summary>
///   Compares two versions using Semantic Versioning rules, where a revision of
///   0 is considered higher than a revision of &gt; 0 or &lt; 0.
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

        if (x.Build != y.Build)
            return x.Build.CompareTo(y.Build);

        if (x.Revision is 0 && y.Revision is 0)
            return 0;

        if (x.Revision is 0 && y.Revision is not 0)
            return 1;

        if (x.Revision is not 0 && y.Revision is 0)
            return -1;

        return x.Revision.CompareTo(y.Revision);
    }
}
