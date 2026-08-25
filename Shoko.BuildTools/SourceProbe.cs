using Microsoft.Build.Evaluation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Shoko.BuildTools;

/// <summary>
///   Scans plugin source files using Roslyn syntax trees to discover Shoko
///   service interface implementations and auto-generate tags, without
///   compiling or loading the assembly.
/// </summary>
internal static class SourceProbe
{
    // Known Shoko service interfaces → tag names.
    private static readonly Dictionary<string, string[]> InterfaceTagMap = new()
    {
        ["IReleaseInfoProvider"] = ["release-provider"],
        ["IHashProvider"] = ["hash-provider"],
        ["IRelocationProvider"] = ["relocation-provider"],
        ["IImageCrossReferenceResolver"] = ["image-cross-reference-resolver"],
        ["IManagedFolderIgnoreRule"] = ["managed-folder-ignore-rule"],
        ["IResourceResolver"] = ["resource-resolver"],
        ["ISupplementaryMetadataProvider"] = ["supplementary-metadata-provider"],
        ["IHostedService"] = ["hosted-service"],
        ["IPluginServiceRegistration"] = ["service-registration"],
        ["IPluginApplicationRegistration"] = ["application-registration"],
    };

    /// <summary>
    ///   Scan the project's source files for Shoko service interface
    ///   implementations and return the set of auto-detected tags.
    /// </summary>
    public static IReadOnlyList<string> DiscoverTags(
        Project project,
        string projectDir,
        IReadOnlySet<string>? existingTags = null)
    {
        var discovered = new List<string>();
        var existing = existingTags ?? new HashSet<string>();

        var sourceFiles = project.GetItems("Compile")
            .Select(i => i.EvaluatedInclude)
            .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var sourceFile in sourceFiles)
        {
            var fullPath = Path.IsPathRooted(sourceFile)
                ? sourceFile
                : Path.GetFullPath(sourceFile, projectDir);

            if (!File.Exists(fullPath))
                continue;

            var source = File.ReadAllText(fullPath);
            var tree = CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default.WithKind(SourceCodeKind.Regular));
            var root = tree.GetRoot();

            foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (classDecl.BaseList is null)
                    continue;

                foreach (var baseType in classDecl.BaseList.Types)
                {
                    // Walk the type name: for `IReleaseInfoProvider<...>` just use "IReleaseInfoProvider"
                    var typeName = baseType.Type switch
                    {
                        IdentifierNameSyntax id => id.Identifier.Text,
                        GenericNameSyntax gen => gen.Identifier.Text,
                        QualifiedNameSyntax qn => qn.Right switch
                        {
                            IdentifierNameSyntax qid => qid.Identifier.Text,
                            GenericNameSyntax qgen => qgen.Identifier.Text,
                            _ => qn.ToString()
                        },
                        _ => baseType.Type.ToString()
                    };

                    // Strip leading namespace/qualifier if present
                    var dotIdx = typeName.LastIndexOf('.');
                    if (dotIdx >= 0)
                        typeName = typeName[(dotIdx + 1)..];

                    // Direct mapping
                    if (InterfaceTagMap.TryGetValue(typeName, out var tags))
                    {
                        foreach (var tag in tags)
                        {
                            if (!existing.Contains(tag) && !discovered.Contains(tag))
                                discovered.Add(tag);
                        }
                        continue;
                    }
                }
            }
        }

        return discovered;
    }
}
