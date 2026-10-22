using System.IO;
using Shoko.Server.Plugin;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Plugin;

/// <summary>
/// The built-in ignore rule applies <c>Import.ExcludeExpressions</c> to directories
/// as well as files, so a matching directory prunes its whole subtree from a scan.
/// </summary>
public sealed class CoreIgnoreRuleTests
{
    [Fact]
    public void FileMatchingAnExclusionIsIgnored()
    {
        var rule = CreateRule(@"[\\/]!Ignored[\\/]");

        Assert.True(rule.ShouldIgnore(null!, new FileInfo("/library/!Ignored/Series/file.mkv")));
    }

    [Fact]
    public void DirectoryMatchingAnExclusionIsIgnored()
    {
        var rule = CreateRule(@"[\\/]!Ignored[\\/]");

        Assert.True(rule.ShouldIgnore(null!, new DirectoryInfo("/library/!Ignored")));
    }

    [Fact]
    public void DirectoryUnderAnExcludedDirectoryIsIgnored()
    {
        var rule = CreateRule(@"[\\/]!Ignored[\\/]");

        Assert.True(rule.ShouldIgnore(null!, new DirectoryInfo("/library/!Ignored/Series")));
    }

    [Fact]
    public void DirectoryNamedByATrailingSeparatorPatternIsIgnored()
    {
        // The shipped defaults are written this way, so the rule has to test a directory
        // with its separator appended or the walk would descend one level before pruning.
        var rule = CreateRule(@"[\\/]\.Trash-\d+[\\/]");

        Assert.True(rule.ShouldIgnore(null!, new DirectoryInfo("/library/.Trash-1000")));
        Assert.True(rule.ShouldIgnore(null!, new DirectoryInfo("/library/.Trash-1000/files/file.mkv")));
    }

    [Fact]
    public void PatternAnchoredAtTheEndOfAPathOnlyMatchesFiles()
    {
        var rule = CreateRule(@"\.mkv$");

        Assert.True(rule.ShouldIgnore(null!, new FileInfo("/library/Series/file.mkv")));
        Assert.False(rule.ShouldIgnore(null!, new DirectoryInfo("/library/Series/file.mkv")));
    }

    [Fact]
    public void UnrelatedDirectoryIsNotIgnored()
    {
        var rule = CreateRule(@"[\\/]!Ignored[\\/]");

        Assert.False(rule.ShouldIgnore(null!, new DirectoryInfo("/library/Series/Season 1")));
    }

    [Fact]
    public void DefaultExclusionsDoNotIgnoreAnOrdinaryLibraryDirectory()
    {
        var rule = new CoreIgnoreRule(new StubSettingsProvider(new ServerSettings()));

        Assert.False(rule.ShouldIgnore(null!, new DirectoryInfo("/library/Series")));
    }

    [Fact]
    public void EmptyExclusionListIgnoresNothing()
    {
        var settings = new ServerSettings();
        settings.Import.Exclude.Clear();
        settings.Import.ResetExcludeRegexes();
        var rule = new CoreIgnoreRule(new StubSettingsProvider(settings));

        Assert.False(rule.ShouldIgnore(null!, new DirectoryInfo("/library/!Ignored")));
    }

    private static CoreIgnoreRule CreateRule(params string[] exclude)
    {
        var settings = new ServerSettings();
        settings.Import.Exclude.Clear();
        settings.Import.Exclude.AddRange(exclude);
        settings.Import.ResetExcludeRegexes();
        return new CoreIgnoreRule(new StubSettingsProvider(settings));
    }
}
