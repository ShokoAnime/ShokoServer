using System;
using System.IO;
using Shoko.BuildTools;
using Xunit;

namespace Shoko.Tests.BuildTools;

/// <summary>
/// Covers the C# source <see cref="AssemblyInfoGenerator.WriteAssemblyInfo"/> emits. The
/// values can now come straight from the command line, where a multi-line overview is
/// ordinary, so they have to survive being put in a string literal.
/// </summary>
public class AssemblyInfoGeneratorTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"shoko-assembly-info-{Guid.NewGuid():N}.cs");

    public void Dispose()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }

    [Fact]
    public void EscapesLineBreaksAndTabs()
    {
        AssemblyInfoGenerator.WriteAssemblyInfo(_path, "any", "", "", "", [], null, pluginDescription: "One.\r\nTwo.\tThree \"four\".");

        Assert.Contains(
            "[assembly: AssemblyMetadata(\"PackageOverview\", \"One.\\r\\nTwo.\\tThree \\\"four\\\".\")]",
            File.ReadAllText(_path));
    }

    [Fact]
    public void WritesDependenciesInTheCanonicalForm()
    {
        var first = new Guid("0f8a1c2e-1111-2222-3333-444455556666");
        var second = new Guid("7d3b9f40-aaaa-bbbb-cccc-ddddeeeeffff");

        AssemblyInfoGenerator.WriteAssemblyInfo(
            _path,
            "any",
            "",
            "",
            "",
            [new PluginDependencyEntry(first, "^1.2.0", false), new PluginDependencyEntry(second, ">=2.0", true)],
            null);

        Assert.Contains(
            $"[assembly: AssemblyMetadata(\"PackageDependencies\", \"{first:D}@^1.2.0,{second:D}@>=2.0:optional\")]",
            File.ReadAllText(_path));
    }
}
