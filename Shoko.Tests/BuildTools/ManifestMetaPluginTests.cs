using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Newtonsoft.Json;
using Shoko.BuildTools;
using Shoko.Server.Plugin.Models;
using Xunit;

namespace Shoko.Tests.BuildTools;

/// <summary>
///   Covers the meta plugin mark on its way from the built assembly
///   through <c>shoko-build</c>'s release entry to the server.
/// </summary>
public class ManifestMetaPluginTests : IDisposable
{
    private readonly string _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"shoko-manifest-metadata-{Guid.NewGuid():N}")).FullName;

    public void Dispose()
        => Directory.Delete(_root, recursive: true);

    private string Compile(string? isMeta)
    {
        var stamp = isMeta is null ? string.Empty : $"[assembly: System.Reflection.AssemblyMetadata(\"PackageIsMeta\", \"{isMeta}\")]";
        var compilation = CSharpCompilation.Create(
            "Example",
            [CSharpSyntaxTree.ParseText($"[assembly: System.Reflection.AssemblyVersion(\"1.0.0.0\")]\n{stamp}")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var path = Path.Combine(_root, "Example.dll");
        using var stream = File.Create(path);
        Assert.True(compilation.Emit(stream).Success);
        return path;
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(null, null)]
    public void ReadsTheMarkFromTheBuiltAssembly(string? stamped, bool? expected)
    {
        Assert.Equal(expected, AssemblyInfoGenerator.ReadAssemblyMetadata(Compile(stamped)).IsMeta);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void TheReleaseEntryCarriesTheMarkToTheServer(bool? isMeta)
    {
        var path = Path.Combine(_root, "manifest.json");
        var manifest = new Manifest { ID = Guid.NewGuid(), Name = "Example" };
        ManifestManager.UpdateOrAddRelease(manifest, new Version(1, 0, 0), new Version(6, 0, 0), "any", [], isMeta);
        ManifestManager.SyncIsMeta(manifest);
        ManifestManager.Save(manifest, path);

        var json = File.ReadAllText(path);
        Assert.Equal(isMeta is not null, json.Contains("\"is_meta\""));
        var loaded = ManifestManager.Load(path);
        Assert.Equal(isMeta, loaded.IsMeta);
        Assert.Equal(isMeta, loaded.Releases!.Single().IsMeta);

        var remote = JsonConvert.DeserializeObject<RemotePackageManifestInfo>(json)!;
        Assert.Equal(isMeta, remote.IsMeta);
        Assert.Equal(isMeta, remote.Releases!.Single().IsMeta);
    }

    [Theory]
    [InlineData(new[] { "true", "true" }, true)]
    [InlineData(new[] { "true", "false" }, false)]
    [InlineData(new[] { "true", null }, null)]
    [InlineData(new[] { null, "false" }, false)]
    [InlineData(new string?[0], null)]
    public void ThePackageIsMetaWhenEveryReleaseIs(string?[] releases, bool? expected)
    {
        var manifest = new Manifest
        {
            ID = Guid.NewGuid(),
            Name = "Example",
            IsMeta = true,
            Releases = releases
                .Select((isMeta, index) => new ManifestRelease { Version = $"1.0.{index}", IsMeta = isMeta is null ? null : bool.Parse(isMeta) })
                .ToList(),
        };

        ManifestManager.SyncIsMeta(manifest);

        Assert.Equal(expected, manifest.IsMeta);
    }
}
