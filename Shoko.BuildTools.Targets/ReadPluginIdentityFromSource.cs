using System.Runtime.Loader;
using Microsoft.Build.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Shoko.BuildTools.Tasks;

/// <summary>
///   Reads plugin identity (ID, Name, Description) from source files using
///   Roslyn syntax trees, falling back to calling UuidUtility.GetV5 via
///   reflection when the GUID is computed from a type name.
/// </summary>
public class ReadPluginIdentityFromSource : PluginMetadataTask
{
    /// <summary>
    ///   How deep the ID is followed through the fields and properties it
    ///   names before giving up.
    /// </summary>
    private const int MaxMemberDepth = 4;

    /// <summary>
    ///   Path to search for the Shoko.Abstractions assembly in NuGet cache.
    /// </summary>
    public string? NuGetPackageRoot { get; set; }

    /// <summary>
    ///   The project's resolved references, <c>@(ReferencePath)</c>, searched
    ///   for the Shoko.Abstractions assembly before the NuGet cache, so a
    ///   project reference to it is found too.
    /// </summary>
    public ITaskItem[]? ReferencePaths { get; set; }

    public override bool Execute()
    {
        try
        {
            var csFiles = Directory.GetFiles(ProjectDir, "*.cs", SearchOption.AllDirectories);
            Guid? pluginId = null;
            string? pluginName = null;
            string? pluginDescription = null;
            string? pluginClassFullName = null;

            foreach (var file in csFiles)
            {
                var source = File.ReadAllText(file);
                var tree = CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default.WithKind(SourceCodeKind.Regular));
                var root = tree.GetRoot();

                foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                {
                    if (!ImplementsIPlugin(classDecl))
                        continue;

                    var fullName = GetFullName(classDecl);

                    // Extract ID, Name, Description from property declarations
                    Guid? candidateId = null;
                    string? candidateName = null;
                    string? candidateDesc = null;

                    foreach (var member in classDecl.Members)
                    {
                        if (member is PropertyDeclarationSyntax prop)
                        {
                            var propName = prop.Identifier.Text;
                            var value = GetExpressionValue(GetPropertyExpression(prop));

                            switch (propName)
                            {
                                case "ID" when value is not null && Guid.TryParse(value, out var g):
                                    candidateId = g;
                                    break;
                                case "ID":
                                    candidateId = ResolveGuid(classDecl, GetPropertyExpression(prop), 0);
                                    break;
                                case "Name":
                                    candidateName = value;
                                    break;
                                case "Description":
                                    candidateDesc = value;
                                    break;
                            }
                        }
                    }

                    // Also check static fields that give the ID (e.g., StaticID = GetV5(...) or new Guid("..."))
                    candidateId ??= TryResolveGetV5FromField(classDecl);

                    // Take the first result with at least an ID; prefer results with more fields
                    if (candidateId.HasValue)
                    {
                        pluginId = candidateId;
                        pluginName ??= candidateName;
                        pluginDescription ??= candidateDesc;
                        pluginClassFullName ??= fullName;
                    }
                }
            }

            // The Plugin* properties still apply without an IPlugin in sight.
            if (pluginClassFullName is null)
                Log.LogMessage(MessageImportance.Low, "No IPlugin implementation found in source files.");

            return WriteMetadata(nameof(ReadPluginIdentityFromSource), pluginId, pluginName, pluginDescription, null, null, "PluginDependencies");
        }
        catch (Exception ex)
        {
            Log.LogError("Failed to read plugin identity from source: {0}", ex.Message);
            return false;
        }
    }

    private static bool ImplementsIPlugin(ClassDeclarationSyntax classDecl)
    {
        if (classDecl.BaseList is null)
            return false;

        return classDecl.BaseList.Types.Any(bt =>
        {
            var name = bt.Type switch
            {
                IdentifierNameSyntax id => id.Identifier.Text,
                GenericNameSyntax gen => gen.Identifier.Text,
                QualifiedNameSyntax qn => qn switch
                {
                    { Right: IdentifierNameSyntax rid } => rid.Identifier.Text,
                    { Right: GenericNameSyntax rgen } => rgen.Identifier.Text,
                    _ => null,
                },
                _ => null,
            };
            return name == "IPlugin";
        });
    }

    private static string GetFullName(ClassDeclarationSyntax classDecl)
    {
        var parts = new List<string> { classDecl.Identifier.Text };
        var parent = classDecl.Parent;
        while (parent is BaseNamespaceDeclarationSyntax ns)
        {
            parts.Insert(0, ns.Name.ToString());
            parent = ns.Parent;
        }
        while (parent is ClassDeclarationSyntax parentClass)
        {
            parts.Insert(0, parentClass.Identifier.Text);
            parent = parentClass.Parent;
        }
        return string.Join(".", parts);
    }

    /// <summary>
    ///   The expression a property gives its value with: its initializer,
    ///   its expression body, or its getter's expression body or single
    ///   <c>return</c>.
    /// </summary>
    private static ExpressionSyntax? GetPropertyExpression(PropertyDeclarationSyntax prop)
    {
        if (prop.Initializer?.Value is { } initializer)
            return initializer;
        if (prop.ExpressionBody?.Expression is { } body)
            return body;

        var getter = prop.AccessorList?.Accessors.FirstOrDefault(accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration));
        if (getter?.ExpressionBody?.Expression is { } getterBody)
            return getterBody;

        return getter?.Body?.Statements is [ReturnStatementSyntax { Expression: { } returned }] ? returned : null;
    }

    private static string? GetExpressionValue(ExpressionSyntax? expr)
    {
        if (expr is null)
            return null;

        // String literal: "value"
        if (expr is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.StringLiteralExpression))
            return lit.Token.ValueText;

        // new Guid("...")
        if (expr is ImplicitObjectCreationExpressionSyntax || expr is ObjectCreationExpressionSyntax)
        {
            var argList = expr.DescendantNodes().OfType<ArgumentListSyntax>().FirstOrDefault();
            if (argList?.Arguments.Count == 1)
            {
                var arg = argList.Arguments[0].Expression;
                if (arg is LiteralExpressionSyntax argLit && argLit.IsKind(SyntaxKind.StringLiteralExpression))
                    return argLit.Token.ValueText;
            }
        }

        // nameof(Plugin)
        if (expr is InvocationExpressionSyntax inv)
        {
            var invoked = inv.Expression.ToString();
            if (invoked == "nameof" && inv.ArgumentList?.Arguments.Count > 0)
                return inv.ArgumentList.Arguments[0].ToString();
        }

        // Ternary: condition ? "value1" : "value2"
        if (expr is ConditionalExpressionSyntax ternary)
        {
            var whenTrue = GetExpressionValue(ternary.WhenTrue);
            var whenFalse = GetExpressionValue(ternary.WhenFalse);
            return whenTrue ?? whenFalse;
        }

        return null;
    }

    /// <summary>
    ///   Resolves the GUID an ID expression gives: a literal, a call to
    ///   GetV5, or a field or property of the class that gives one of those.
    /// </summary>
    /// <param name="classDecl">The plugin class.</param>
    /// <param name="expr">The expression.</param>
    /// <param name="depth">How many members were followed to get here.</param>
    /// <returns>The GUID, or <c>null</c> when it cannot be worked out from source.</returns>
    private Guid? ResolveGuid(ClassDeclarationSyntax classDecl, ExpressionSyntax? expr, int depth)
    {
        if (expr is null || depth > MaxMemberDepth)
            return null;

        if (GetExpressionValue(expr) is { } literal && Guid.TryParse(literal, out var guid))
            return guid;

        // GetV5(typeof(X).FullName!), guessing this class when the argument cannot be read.
        if (ContainsGetV5(expr))
            return CallGetV5(ExtractGetV5TypeName(expr, classDecl) ?? GetFullName(classDecl));

        // A field or property of this class: StaticID, or Plugin.StaticID.
        var memberName = expr switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax owner, Name: IdentifierNameSyntax name }
                when owner.Identifier.Text == classDecl.Identifier.Text => name.Identifier.Text,
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: IdentifierNameSyntax name } => name.Identifier.Text,
            _ => null,
        };
        if (memberName is null)
            return null;

        foreach (var member in classDecl.Members)
        {
            switch (member)
            {
                case FieldDeclarationSyntax field:
                    foreach (var variable in field.Declaration.Variables)
                        if (variable.Identifier.Text == memberName)
                            return ResolveGuid(classDecl, variable.Initializer?.Value, depth + 1);
                    break;

                case PropertyDeclarationSyntax prop when prop.Identifier.Text == memberName:
                    return ResolveGuid(classDecl, GetPropertyExpression(prop), depth + 1);
            }
        }

        return null;
    }

    /// <summary>
    ///   Check if a static field (like <c>StaticID = GetV5(typeof(X).FullName!)</c> or
    ///   <c>StaticID = new Guid("...")</c>) provides the plugin ID, and resolve it.
    /// </summary>
    private Guid? TryResolveGetV5FromField(ClassDeclarationSyntax classDecl)
    {
        foreach (var member in classDecl.Members)
        {
            if (member is not FieldDeclarationSyntax field || !field.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
                continue;

            foreach (var variable in field.Declaration.Variables)
                if (ResolveGuid(classDecl, variable.Initializer?.Value, MaxMemberDepth) is { } guid)
                    return guid;
        }

        return null;
    }

    private static bool ContainsGetV5(SyntaxNode node)
        => node.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any(inv => inv.Expression.ToString().Contains("GetV5"));

    private static string? ExtractGetV5TypeName(SyntaxNode member, ClassDeclarationSyntax classDecl)
    {
        // Look for patterns like:
        //   GetV5(typeof(Plugin).FullName!)
        //   UuidUtility.GetV5(typeof(Plugin).FullName!)
        foreach (var inv in member.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
        {
            var methodName = inv.Expression.ToString();
            if (!methodName.Contains("GetV5"))
                continue;

            // Get the first argument
            if (inv is not { ArgumentList.Arguments.Count: > 0 })
                continue;

            var arg = inv.ArgumentList.Arguments[0].Expression;
            if (arg is PostfixUnaryExpressionSyntax outer && outer.IsKind(SyntaxKind.SuppressNullableWarningExpression))
                arg = outer.Operand;

            // typeof(Plugin).FullName or typeof(Plugin).FullName!
            if (arg is MemberAccessExpressionSyntax memberAccess)
            {
                var inner = memberAccess.Expression;
                // Strip null-forgiving operator
                if (inner is PostfixUnaryExpressionSyntax postfix && postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression))
                    inner = postfix.Operand;

                if (inner is TypeOfExpressionSyntax typeofExpr)
                {
                    return typeofExpr.Type switch
                    {
                        // The plugin class itself, named without its namespace.
                        IdentifierNameSyntax id when id.Identifier.Text == classDecl.Identifier.Text => GetFullName(classDecl),
                        IdentifierNameSyntax id => id.Identifier.Text,
                        QualifiedNameSyntax qn => qn.ToString(),
                        _ => null,
                    };
                }
            }

            // Direct type name as string
            if (arg is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.StringLiteralExpression))
                return lit.Token.ValueText;
        }

        return null;
    }

    /// <summary>
    ///   Finds the Shoko.Abstractions assembly: among the project's resolved
    ///   references first, then in the NuGet cache.
    /// </summary>
    /// <returns>The assembly's path, or <c>null</c> when it is in neither.</returns>
    private string? FindAbstractionsAssembly()
    {
        var referenced = ReferencePaths?
            .Select(item => item.GetMetadata("FullPath") is { Length: > 0 } fullPath ? fullPath : item.ItemSpec)
            .FirstOrDefault(path => string.Equals(Path.GetFileName(path), "Shoko.Abstractions.dll", StringComparison.OrdinalIgnoreCase) && File.Exists(path));
        if (referenced is not null)
            return referenced;

        var root = NuGetPackageRoot is { Length: > 0 }
            ? NuGetPackageRoot
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        var abstractionsDir = new[] { "shoko.abstractions", "Shoko.Abstractions" }
            .Select(name => Path.Combine(root, name))
            .Where(Directory.Exists)
            .SelectMany(Directory.GetDirectories)
            .OrderByDescending(dir => dir)
            .FirstOrDefault();

        return abstractionsDir is null
            ? null
            : Directory.GetFiles(abstractionsDir, "Shoko.Abstractions.dll", SearchOption.AllDirectories).FirstOrDefault();
    }

    private Guid? CallGetV5(string typeName)
    {
        try
        {
            var dllPath = FindAbstractionsAssembly();
            if (dllPath is null)
            {
                Log.LogMessage(MessageImportance.Low, "Shoko.Abstractions.dll not found among the references or in the NuGet cache.");
                return null;
            }

            // Load in an isolated collectible context so we can call GetV5
            var alc = new AssemblyLoadContext("GetV5Loader", isCollectible: true);
            try
            {
                var abstractionsAssembly = alc.LoadFromAssemblyPath(dllPath);
                var uuidUtilType = abstractionsAssembly.GetType("Shoko.Abstractions.Utilities.UuidUtility");

                if (uuidUtilType is null)
                {
                    Log.LogMessage(MessageImportance.Low, "UuidUtility type not found.");
                    return null;
                }

                var getV5Method = uuidUtilType.GetMethod("GetV5", [typeof(string), typeof(Guid)]);
                if (getV5Method is null)
                {
                    Log.LogMessage(MessageImportance.Low, "UuidUtility.GetV5 method not found.");
                    return null;
                }

                var result = getV5Method.Invoke(null, [typeName, Guid.Empty]);
                if (result is Guid guid)
                    return guid;
            }
            finally
            {
                alc.Unload();
            }
        }
        catch (Exception ex)
        {
            Log.LogMessage(MessageImportance.Low, "Failed to invoke GetV5 via reflection: {0}", ex.Message);
        }

        return null;
    }
}
