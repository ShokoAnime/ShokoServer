using System;
using System.Collections.Generic;
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

    [Fact]
    public void Parse_DefaultsToRequired()
        => Assert.Equal([new PluginDependencyEntry(_first, "^1.2.0", false)], ParseClean($"{_first}@^1.2.0"));

    [Theory]
    [InlineData("optional", true)]
    [InlineData("Optional", true)]
    [InlineData("required", false)]
    [InlineData("REQUIRED", false)]
    public void Parse_ReadsTheMarker(string marker, bool isOptional)
        => Assert.Equal([new PluginDependencyEntry(_first, ">=2.0", isOptional)], ParseClean($"{_first}@>=2.0:{marker}"));

    [Fact]
    public void Parse_IgnoresWhitespaceAroundEverySeparator()
        => Assert.Equal(
            [new PluginDependencyEntry(_first, "^1.2.0", true), new PluginDependencyEntry(_second, ">=2.0", false)],
            ParseClean($"  {_first} @ ^1.2.0 : optional ,\n\t{_second}@>=2.0\n"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(", ,\n,")]
    public void Parse_NothingButSeparatorsIsAnEmptyList(string value)
        => Assert.Empty(ParseClean(value));

    [Fact]
    public void Parse_SkipsEmptyEntries()
        => Assert.Equal(
            [new PluginDependencyEntry(_first, "~1.0.0", false), new PluginDependencyEntry(_second, "1.5.0", false)],
            ParseClean($",{_first}@~1.0.0,,{_second}@1.5.0,"));

    [Fact]
    public void Parse_ReadsTheLegacyForm()
        => Assert.Equal(
            [new PluginDependencyEntry(_first, ">=1.0.0", false), new PluginDependencyEntry(_second, "^2.0.0", true)],
            ParseClean($"{_first}:>=1.0.0,{_second}:^2.0.0:true"));

    [Fact]
    public void Parse_TellsTheFormsApartPerEntry()
        => Assert.Equal(
            [new PluginDependencyEntry(_first, ">=1.0.0", true), new PluginDependencyEntry(_second, "^2.0.0", true)],
            ParseClean($"{_first}:>=1.0.0:True,{_second}@^2.0.0:optional"));

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
