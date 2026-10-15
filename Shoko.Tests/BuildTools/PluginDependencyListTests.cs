using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.BuildTools;
using Xunit;

namespace Shoko.Tests.BuildTools;

/// <summary>
/// Covers <see cref="PluginDependencyList"/>, the one parser and formatter behind
/// <c>--dependencies</c>, the <c>PluginDependencies</c> MSBuild property and the
/// <c>PackageDependencies</c> assembly metadata the server reads.
/// </summary>
public class PluginDependencyListTests
{
    private static readonly Guid _first = new("0f8a1c2e-1111-2222-3333-444455556666");

    private static readonly Guid _second = new("7d3b9f40-aaaa-bbbb-cccc-ddddeeeeffff");

    private static List<PluginDependencyEntry> ParseClean(string value)
    {
        var dependencies = PluginDependencyList.Parse(value, out var errors);
        Assert.Empty(errors);
        return dependencies;
    }

    [Theory]
    [InlineData("{0}@^1.2.0", new[] { "^1.2.0" }, new[] { false })] // a missing marker means required
    [InlineData("  {0} @ ^1.2.0 : optional ,\n\t{1}@>=2.0\n", new[] { "^1.2.0", ">=2.0" }, new[] { true, false })] // whitespace around every separator
    [InlineData(",{0}@~1.0.0,,{1}@1.5.0,", new[] { "~1.0.0", "1.5.0" }, new[] { false, false })] // empty entries are skipped
    [InlineData("{0}:>=1.0.0,{1}:^2.0.0:true", new[] { ">=1.0.0", "^2.0.0" }, new[] { false, true })] // the legacy form
    [InlineData("{0}:>=1.0.0:True,{1}@^2.0.0:optional", new[] { ">=1.0.0", "^2.0.0" }, new[] { true, true })] // the forms apart per entry
    public void Parse_ReadsTheEntriesOfAList(string list, string[] ranges, bool[] optionals)
    {
        Guid[] ids = [_first, _second];
        var expected = ranges.Select((range, index) => new PluginDependencyEntry(ids[index], range, optionals[index]));

        Assert.Equal(expected, ParseClean(string.Format(list, _first, _second)));
    }

    [Theory]
    [InlineData("optional", true)]
    [InlineData("Optional", true)]
    [InlineData("required", false)]
    [InlineData("REQUIRED", false)]
    public void Parse_ReadsTheMarker(string marker, bool isOptional)
        => Assert.Equal([new PluginDependencyEntry(_first, ">=2.0", isOptional)], ParseClean($"{_first}@>=2.0:{marker}"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(", ,\n,")]
    public void Parse_NothingButSeparatorsIsAnEmptyList(string value)
        => Assert.Empty(ParseClean(value));

    [Theory]
    [InlineData("{0}", "has no version range")]
    [InlineData("not-a-guid@^1.0", "is not a GUID")]
    [InlineData("{0}@", "empty version range")]
    [InlineData("{0}@ :optional", "empty version range")]
    [InlineData("{0}@^1.0:true", "expected ':optional' or ':required'")]
    [InlineData("{0}@^1.0:", "expected ':optional' or ':required'")]
    [InlineData("{0}:^1.0:optional", "legacy form expects ':true' or ':false'")]
    [InlineData("{0}@^1.0:optional:required", "more than one ':'")]
    public void Parse_ReportsAndSkipsABadEntry(string entryFormat, string expectedError)
    {
        var entry = string.Format(entryFormat, _first);

        var dependencies = PluginDependencyList.Parse($"{entry},{_second}@^1.0", out var errors);

        Assert.Equal([new PluginDependencyEntry(_second, "^1.0", false)], dependencies);
        Assert.Contains(expectedError, Assert.Single(errors));
    }

    [Fact]
    public void Format_WritesTheCanonicalForm()
        => Assert.Equal(
            $"{_first:D}@^1.2.0,{_second:D}@>=2.0:optional",
            PluginDependencyList.Format([new PluginDependencyEntry(_first, " ^1.2.0 ", false), new PluginDependencyEntry(_second, ">=2.0", true)]));

    [Fact]
    public void Format_OfNothingIsEmpty()
        => Assert.Equal(string.Empty, PluginDependencyList.Format([]));

    [Fact]
    public void Format_RoundTripsThroughParse()
    {
        List<PluginDependencyEntry> dependencies =
        [
            new PluginDependencyEntry(_first, "~1.2.3", true),
            new PluginDependencyEntry(_second, "1.2.3.4", false),
        ];

        Assert.Equal(dependencies, ParseClean(PluginDependencyList.Format(dependencies)));
    }

    [Theory]
    [InlineData(">=1.0.0")]
    [InlineData("^1.0")]
    [InlineData("~1.2.3")]
    [InlineData("1.5.0")]
    [InlineData("1.2.3.4")]
    public void Validate_AcceptsEveryRangeTheServerEvaluates(string range)
        => Assert.Empty(PluginDependencyList.Validate([new PluginDependencyEntry(_first, range, false)]));

    [Theory]
    [InlineData(">1.0.0")]
    [InlineData("<=1.0.0")]
    [InlineData("1")]
    [InlineData("^")]
    [InlineData("latest")]
    [InlineData("1.0.0-dev.1")]
    public void Validate_RejectsARangeTheServerCannotEvaluate(string range)
        => Assert.Contains("is not a version range", Assert.Single(PluginDependencyList.Validate([new PluginDependencyEntry(_first, range, false)])));

    [Fact]
    public void Validate_RejectsAPluginListedTwice()
        => Assert.Contains(
            "listed more than once",
            Assert.Single(PluginDependencyList.Validate([new PluginDependencyEntry(_first, "^1.0", false), new PluginDependencyEntry(_first, "^2.0", true)])));
}
