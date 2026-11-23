using System;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shoko.Server.Plugin;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Plugin;

/// <summary>
///   A plugin built against an older version of an interface it implements
///   is named as built for another version, not as missing dependencies.
/// </summary>
public sealed class PluginChangedInterfaceTests : IDisposable
{
    private readonly string _root = Directory.CreateDirectory(Path.Join(Path.GetTempPath(), $"shoko-changed-interface-{Guid.NewGuid():N}")).FullName;

    public void Dispose()
        => TemporaryDirectory.Delete(_root);

    [Fact]
    public void AChangedInterfaceMember_IsNamed()
    {
        var oldContract = Compile("old", "Contract", "namespace Contract; public interface IRule { bool ShouldIgnore(int entry); }");
        var plugin = Compile("plugin", "Plugin", "namespace Plugin; public class Rule : Contract.IRule { public bool ShouldIgnore(int entry) => false; }", oldContract);
        var newContract = Compile("new", "Contract", "namespace Contract; public interface IRule { bool ShouldIgnore(string entry); }");

        var context = new AssemblyLoadContext("changed-interface", isCollectible: true);
        try
        {
            context.LoadFromAssemblyPath(newContract);
            var exception = Assert.Throws<TypeLoadException>(() => context.LoadFromAssemblyPath(plugin).GetExportedTypes());

            var reason = PluginManager.DescribeMissingImplementation(exception.Message);

            Assert.NotNull(reason);
            Assert.Contains("Plugin.Rule", reason);
            Assert.Contains("ShouldIgnore", reason);
        }
        finally
        {
            context.Unload();
        }
    }

    [Fact]
    public void AnotherTypeLoadFailure_IsNotTakenForOne()
        => Assert.Null(PluginManager.DescribeMissingImplementation("Could not load type 'Contract.IRule' from assembly 'Contract, Version=1.0.0.0'."));

    private string Compile(string folder, string assemblyName, string source, params string[] references)
    {
        var directory = Directory.CreateDirectory(Path.Join(_root, folder)).FullName;
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Concat(references)
                .Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var path = Path.Join(directory, $"{assemblyName}.dll");
        using (var stream = File.Create(path))
        {
            var result = compilation.Emit(stream);
            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        }

        return path;
    }
}
