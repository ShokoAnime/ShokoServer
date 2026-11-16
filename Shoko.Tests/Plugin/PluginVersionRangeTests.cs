using System;
using Shoko.Server.Plugin;
using Xunit;

namespace Shoko.Tests.Plugin;

/// <summary>
///   Covers dependency version ranges ordering versions as semantic
///   versioning does, a fourth part above <c>0</c> being a prerelease.
/// </summary>
public class PluginVersionRangeTests
{
    [Theory]
    // A release satisfies a minimum of one of its own prereleases, in either spelling.
    [InlineData(">=0.1.0.3", "0.1.0", true)]
    [InlineData(">=0.1.0-dev.3", "0.1.0", true)]
    [InlineData(">=0.1.0.3", "0.1.0.4", true)]
    [InlineData(">=0.1.0.3", "0.1.0.2", false)]
    // A prerelease is below its release.
    [InlineData(">=0.1.0", "0.1.0.3", false)]
    [InlineData(">=0.1.0", "0.1.1.1", true)]
    [InlineData("^0.29.0-dev.8", "0.29.0", true)]
    [InlineData("~1.2.0.1", "1.3.0", false)]
    [InlineData("0.1.0-dev.3", "0.1.0.3", true)]
    // Three-part ranges keep their meaning, with missing parts read as 0.
    [InlineData(">=1.2", "1.2.0", true)]
    [InlineData("^1.2.0", "2.0.0", false)]
    [InlineData("1.2.0", "1.2", true)]
    public void ComparesBySemanticVersioning(string range, string candidate, bool expected)
        => Assert.Equal(expected, PluginVersionRange.IsSatisfied(range, Version.Parse(candidate)));

    [Theory]
    [InlineData(">=0.1.0-dev.3", true)]
    [InlineData("^1.2.3.4", true)]
    [InlineData(">=one", false)]
    [InlineData("0.1.0-dev", false)]
    [InlineData("0.1.0-beta.2", false)]
    public void ReadsBothPrereleaseSpellings(string range, bool expected)
        => Assert.Equal(expected, PluginDependencyList.IsValidVersionRange(range));
}
