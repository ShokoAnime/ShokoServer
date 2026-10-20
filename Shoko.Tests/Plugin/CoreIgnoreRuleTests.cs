using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Shoko.Server.Plugin;
using Shoko.Server.Services;
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
    #region Unit behaviour

    [Fact]
    public void FileMatchingAnExclusionIsIgnored()
    {
        var rule = CreateRule(@"[\\/]!Relay[\\/]");

        Assert.True(rule.ShouldIgnore(null!, new FileInfo("/library/!Relay/Series/file.mkv")));
    }

    [Fact]
    public void DirectoryMatchingAnExclusionIsIgnored()
    {
        var rule = CreateRule(@"[\\/]!Relay[\\/]");

        Assert.True(rule.ShouldIgnore(null!, new DirectoryInfo("/library/!Relay")));
    }

    [Fact]
    public void DirectoryUnderAnExcludedDirectoryIsIgnored()
    {
        var rule = CreateRule(@"[\\/]!Relay[\\/]");

        Assert.True(rule.ShouldIgnore(null!, new DirectoryInfo("/library/!Relay/Series")));
    }

    [Fact]
    public void DirectoryWithATrailingSeparatorPatternIsIgnoredByName()
    {
        // The shipped defaults are written this way; a directory's FullName has no
        // trailing separator, so the rule has to try the terminated form too or the
        // walk would descend one level before pruning.
        var rule = CreateRule(@"[\\/]\.Trash-\d+[\\/]");

        Assert.True(rule.ShouldIgnore(null!, new DirectoryInfo("/library/.Trash-1000")));
        Assert.True(rule.ShouldIgnore(null!, new DirectoryInfo("/library/.Trash-1000/files/file.mkv")));
    }

    [Fact]
    public void UnrelatedDirectoryIsNotIgnored()
    {
        var rule = CreateRule(@"[\\/]!Relay[\\/]");

        Assert.False(rule.ShouldIgnore(null!, new DirectoryInfo("/library/Series/Season 1")));
    }

    [Fact]
    public void DefaultExclusionsDoNotIgnoreAnOrdinaryLibraryDirectory()
    {
        var rule = new CoreIgnoreRule(new StubSettingsProvider(new ServerSettings()));

        Assert.False(rule.ShouldIgnore(null!, new DirectoryInfo(Path.Combine(Path.GetTempPath(), "library", "Series"))));
    }

    [Fact]
    public void EmptyExclusionListIgnoresNothing()
    {
        var settings = new ServerSettings();
        settings.Import.Exclude.Clear();
        settings.Import.ResetExcludeRegexes();
        var rule = new CoreIgnoreRule(new StubSettingsProvider(settings));

        Assert.False(rule.ShouldIgnore(null!, new DirectoryInfo("/library/!Relay")));
    }

    #endregion

    #region Walker integration

    [Fact]
    public void ScanPrunesExcludedDirectorySubtree()
    {
        using var tree = new TemporaryTree();
        var root = tree.Root;
        var excluded = Directory.CreateDirectory(Path.Combine(root, "!Relay")).FullName;
        var included = Directory.CreateDirectory(Path.Combine(root, "Series")).FullName;
        File.WriteAllText(Path.Combine(excluded, "file.mkv"), "");
        Directory.CreateDirectory(Path.Combine(excluded, "Season 1"));
        File.WriteAllText(Path.Combine(excluded, "Season 1", "nested.mkv"), "");
        File.WriteAllText(Path.Combine(included, "file.mkv"), "");

        var rule = CreateRule($@"[\\/]{System.Text.RegularExpressions.Regex.Escape("!Relay")}[\\/]");
        var helpers = new FileSystemHelpers(NullLogger<FileSystemHelpers>.Instance);
        var paths = helpers.GetFilePaths(
            root,
            recursive: true,
            filter: (path, isDirectory) => !rule.ShouldIgnore(null!, isDirectory ? new DirectoryInfo(path) : new FileInfo(path)),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains(Path.Combine(included, "file.mkv"), paths);
        Assert.DoesNotContain(paths, path => path.StartsWith(excluded, System.StringComparison.Ordinal));
    }

    #endregion

    private static CoreIgnoreRule CreateRule(params string[] exclude)
    {
        var settings = new ServerSettings();
        settings.Import.Exclude.Clear();
        settings.Import.Exclude.AddRange(exclude);
        settings.Import.ResetExcludeRegexes();
        return new CoreIgnoreRule(new StubSettingsProvider(settings));
    }

    private sealed class TemporaryTree : System.IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("shoko-core-ignore-").FullName;

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
                // A leftover temp directory is not worth failing a test over.
            }
        }
    }
}
