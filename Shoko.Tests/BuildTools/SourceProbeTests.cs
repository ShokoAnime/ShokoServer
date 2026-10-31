using System;
using System.IO;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Locator;
using Shoko.BuildTools;
using Xunit;

namespace Shoko.Tests.BuildTools;

/// <summary>
///   Covers that <c>shoko-build</c>'s source probe finds a plugin's service
///   implementations wherever the build was started from.
/// </summary>
public class SourceProbeTests : IDisposable
{
    static SourceProbeTests()
    {
        // Evaluating a project needs MSBuild's own assemblies, which only an SDK holds.
        if (!MSBuildLocator.IsRegistered)
            MSBuildLocator.RegisterDefaults();
    }

    private readonly string _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"shoko-source-probe-{Guid.NewGuid():N}")).FullName;

    public void Dispose()
        => Directory.Delete(_root, recursive: true);

    [Fact]
    public void AProjectInASubfolder_HasItsProvidersFound()
    {
        // The project sits below the folder the build is started from, as when a repository's
        // workflow runs `shoko-build ./Plugin/Plugin.csproj` from its root.
        var projectDir = Directory.CreateDirectory(Path.Combine(_root, "Plugin")).FullName;
        var projectFile = Path.Combine(projectDir, "Plugin.csproj");
        // A plain project naming its one source file: no SDK, whose own assemblies would have to load
        // into the test host, and a relative path, as the SDK's default items are.
        File.WriteAllText(projectFile, """<Project><ItemGroup><Compile Include="Renamer.cs" /></ItemGroup></Project>""");
        File.WriteAllText(Path.Combine(projectDir, "Renamer.cs"), "public class Renamer(int value) : IRelocationProvider<Settings> { }");

        using var collection = new ProjectCollection();
        var project = new Project(projectFile, null, null, collection);

        Assert.NotEqual(projectDir, Directory.GetCurrentDirectory());
        Assert.Contains("relocator", SourceProbe.DiscoverTags(project));
    }
}
