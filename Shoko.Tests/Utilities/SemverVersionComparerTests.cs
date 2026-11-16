using System;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Utilities;
using Xunit;

namespace Shoko.Tests.Utilities;

/// <summary>
///   Covers semantic version ordering, where an unset part counts as 0 and
///   only a revision above 0 is a prerelease.
/// </summary>
public class SemverVersionComparerTests
{
    [Theory]
    [InlineData("0.1.0", "0.1.0.0", 0)]
    [InlineData("0.1", "0.1.0", 0)]
    [InlineData("0.1.0", "0.1.0.3", 1)]
    [InlineData("0.1.0.0", "0.1.0.3", 1)]
    [InlineData("0.1.0.3", "0.1.0.4", -1)]
    [InlineData("0.1.0.4", "0.1.1", -1)]
    public void UnsetPartsCountAsZero(string x, string y, int expected)
        => Assert.Equal(expected, Math.Sign(new SemverVersionComparer().Compare(Version.Parse(x), Version.Parse(y))));

    [Theory]
    [InlineData("0.1", "0.1.0")]
    [InlineData("0.1.0", "0.1.0")]
    [InlineData("0.1.0.3", "0.1.0-dev.3")]
    public void WritesUnsetPartsAsZero(string version, string expected)
        => Assert.Equal(expected, Version.Parse(version).ToSemanticVersioningString());
}
