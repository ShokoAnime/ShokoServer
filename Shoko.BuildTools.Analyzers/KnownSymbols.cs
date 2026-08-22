using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Shoko.BuildTools.Analyzers;

/// <summary>
/// The symbols <see cref="ConfigurationTypeAnalyzer"/> needs, resolved once per compilation.
/// </summary>
internal sealed class KnownSymbols
{
    /// <summary>
    /// <c>Shoko.Abstractions.Config.IConfiguration</c>, if referenced.
    /// </summary>
    public INamedTypeSymbol? Configuration { get; }

    /// <summary>
    /// <c>Shoko.Abstractions.Actions.IExecutableAction</c>, if referenced.
    /// </summary>
    /// <remarks>
    /// An executable action's invocation parameters are its own settable, serialized properties,
    /// walked by the very same schema generator a configuration is, so the same unrenderable shapes
    /// break it identically.
    /// </remarks>
    public INamedTypeSymbol? ExecutableAction { get; }

    /// <summary>
    /// <c>Shoko.Abstractions.UI.Attributes.ListAttribute</c>, if referenced.
    /// </summary>
    public INamedTypeSymbol? ListAttribute { get; }

    /// <summary>
    /// <c>System.ComponentModel.DataAnnotations.KeyAttribute</c>, if referenced.
    /// </summary>
    public INamedTypeSymbol? KeyAttribute { get; }

    /// <summary>
    /// <c>System.Collections.IDictionary</c>, the non-generic one.
    /// </summary>
    public INamedTypeSymbol? NonGenericDictionary { get; }

    /// <summary>
    /// <c>System.Collections.Generic.IDictionary&lt;,&gt;</c>.
    /// </summary>
    public INamedTypeSymbol? GenericDictionary { get; }

    /// <summary>
    /// <c>System.Collections.Generic.IReadOnlyDictionary&lt;,&gt;</c>.
    /// </summary>
    public INamedTypeSymbol? GenericReadOnlyDictionary { get; }

    /// <summary>
    /// <c>System.Text.Json.Serialization.JsonSerializableAttribute</c>, if referenced.
    /// </summary>
    public INamedTypeSymbol? JsonSerializableAttribute { get; }

    /// <summary>
    /// <c>System.Runtime.Serialization.ISerializable</c>.
    /// </summary>
    public INamedTypeSymbol? SerializableInterface { get; }

    /// <summary>
    /// <c>Newtonsoft.Json.JsonIgnoreAttribute</c>, if referenced.
    /// </summary>
    public INamedTypeSymbol? NewtonsoftJsonIgnoreAttribute { get; }

    /// <summary>
    /// <c>System.Text.Json.Serialization.JsonIgnoreAttribute</c>, if referenced.
    /// </summary>
    public INamedTypeSymbol? SystemTextJsonIgnoreAttribute { get; }

    /// <summary>
    /// <c>NJsonSchema.Annotations.JsonSchemaIgnoreAttribute</c>, if referenced.
    /// </summary>
    public INamedTypeSymbol? JsonSchemaIgnoreAttribute { get; }

    /// <summary>
    /// Types that implement a collection interface but are mapped to something other than a JSON
    /// array or a JSON object by the schema generator, so they must not be treated as collections.
    /// </summary>
    public ImmutableArray<INamedTypeSymbol> NonCollectionBaseTypes { get; }

    private KnownSymbols(Compilation compilation, INamedTypeSymbol? configuration, INamedTypeSymbol? executableAction)
    {
        Configuration = configuration;
        ExecutableAction = executableAction;
        ListAttribute = compilation.GetTypeByMetadataName("Shoko.Abstractions.UI.Attributes.ListAttribute");
        KeyAttribute = compilation.GetTypeByMetadataName("System.ComponentModel.DataAnnotations.KeyAttribute");
        NonGenericDictionary = compilation.GetTypeByMetadataName("System.Collections.IDictionary");
        GenericDictionary = compilation.GetTypeByMetadataName("System.Collections.Generic.IDictionary`2");
        GenericReadOnlyDictionary = compilation.GetTypeByMetadataName("System.Collections.Generic.IReadOnlyDictionary`2");
        JsonSerializableAttribute = compilation.GetTypeByMetadataName("System.Text.Json.Serialization.JsonSerializableAttribute");
        SerializableInterface = compilation.GetTypeByMetadataName("System.Runtime.Serialization.ISerializable");
        NewtonsoftJsonIgnoreAttribute = compilation.GetTypeByMetadataName("Newtonsoft.Json.JsonIgnoreAttribute");
        SystemTextJsonIgnoreAttribute = compilation.GetTypeByMetadataName("System.Text.Json.Serialization.JsonIgnoreAttribute");
        JsonSchemaIgnoreAttribute = compilation.GetTypeByMetadataName("NJsonSchema.Annotations.JsonSchemaIgnoreAttribute");
        NonCollectionBaseTypes = Resolve(
            compilation,
            "Newtonsoft.Json.Linq.JToken",
            "System.Text.Json.Nodes.JsonNode");
    }

    /// <summary>
    /// Resolves the symbols for the given compilation, or <see langword="null"/> when the
    /// compilation references neither contract the schema generator starts from.
    /// </summary>
    /// <param name="compilation">The compilation to resolve against.</param>
    /// <returns>The resolved symbols, or <see langword="null"/>.</returns>
    public static KnownSymbols? TryCreate(Compilation compilation)
    {
        var configuration = compilation.GetTypeByMetadataName("Shoko.Abstractions.Config.IConfiguration");
        var executableAction = compilation.GetTypeByMetadataName("Shoko.Abstractions.Actions.IExecutableAction");
        return configuration is null && executableAction is null
            ? null
            : new KnownSymbols(compilation, configuration, executableAction);
    }

    private static ImmutableArray<INamedTypeSymbol> Resolve(Compilation compilation, params string[] metadataNames)
    {
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>(metadataNames.Length);
        foreach (var metadataName in metadataNames)
        {
            if (compilation.GetTypeByMetadataName(metadataName) is { } symbol)
                builder.Add(symbol);
        }
        return builder.ToImmutable();
    }
}
