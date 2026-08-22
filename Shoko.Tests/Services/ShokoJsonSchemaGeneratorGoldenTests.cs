using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Microsoft.Extensions.Logging.Abstractions;
using Shoko.Abstractions.UI;
using Shoko.Abstractions.UI.Elements;
using Shoko.Server.Services.Configuration;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   Pins the serialised schema — <c>x-uiDefinition</c> and all — against a
///   committed golden file.
/// </summary>
/// <remarks>
///   <para>
///     <see cref="ShokoJsonSchemaValidator{TConfig}"/> reads the
///     <c>x-uiDefinition</c> bag back for environment-variable splicing,
///     env-lock enforcement, enum alias resolution and restart-pending
///     detection, so its shape is load-bearing and may not drift. The bag
///     carries nothing beyond those four jobs — presentation lives in
///     <see cref="UiDefinition"/> — so any entry appearing here that the
///     validator does not read is a regression.
///   </para>
///   <para>
///     Set <c>SHOKO_UPDATE_GOLDEN=1</c> to rewrite the goldens after an
///     intentional change.
///   </para>
/// </remarks>
[Collection(ConfigurationSchemaCollection.Name)]
public class ShokoJsonSchemaGeneratorGoldenTests
{
    internal static ShokoJsonSchemaGenerator CreateGenerator()
        => new(ShokoJsonSerializers.CreateNewtonsoftSettings(), ShokoJsonSerializers.CreateSystemTextJsonOptions());

    [Theory]
    [InlineData(typeof(ServerSettings), "ServerSettings")]
    [InlineData(typeof(NewtonsoftTwinConfiguration), "NewtonsoftTwin")]
    [InlineData(typeof(SystemTextJsonTwinConfiguration), "SystemTextJsonTwin")]
    [InlineData(typeof(InheritingConfiguration), "Inheriting")]
    public void Schema_MatchesTheGolden(Type type, string goldenName)
    {
        var actual = CreateGenerator().GetSchemaForType(type).Schema.ToJson().ReplaceLineEndings("\n");
        var goldenPath = Path.Combine(TestPaths.DataDirectory, "Configuration", $"{goldenName}.schema.golden.json");
        if (Environment.GetEnvironmentVariable("SHOKO_UPDATE_GOLDEN") is "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(goldenPath)!);
            File.WriteAllText(goldenPath, actual);
            return;
        }

        Assert.True(File.Exists(goldenPath), $"Missing golden file '{goldenPath}'. Run the suite with SHOKO_UPDATE_GOLDEN=1 to create it.");
        Assert.Equal(File.ReadAllText(goldenPath).ReplaceLineEndings("\n"), actual);
    }
}

/// <summary>
///   A collection cannot hold another collection: every schema node a property
///   produces is filed under one <c>+List</c> and one <c>+Dict</c> marker at
///   most, so the two levels are indistinguishable.
/// </summary>
[Collection(ConfigurationSchemaCollection.Name)]
public class NestedCollectionTests
{
    [Theory]
    [InlineData(typeof(NestedListOfListConfiguration), "List<List<String>>")]
    [InlineData(typeof(NestedListOfRecordConfiguration), "List<Dictionary<String, String>>")]
    [InlineData(typeof(NestedRecordOfRecordConfiguration), "Dictionary<String, Dictionary<String, String>>")]
    [InlineData(typeof(NestedArrayOfArrayConfiguration), "String[][]")]
    public void NestedCollection_IsRejectedWithAnActionableError(Type type, string expectedTypeName)
    {
        var generator = ShokoJsonSchemaGeneratorGoldenTests.CreateGenerator();
        var exception = Assert.Throws<NotSupportedException>(() => generator.GetSchemaForType(type));

        Assert.Contains($"\"{type.Name}.Values\"", exception.Message, StringComparison.Ordinal);
        Assert.Contains(expectedTypeName, exception.Message, StringComparison.Ordinal);
        Assert.Contains("Wrap the inner collection in a class", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADictionaryOfCollections_IsAccepted()
    {
        // The two levels get distinct keys ("+Dict" and "+List"), so nothing
        // collides and the generator produces a usable schema. A dictionary of
        // scalar arrays is an ordinary shape and must keep working.
        AssertDescribes(typeof(NestedRecordOfListConfiguration), "Values");
    }

    [Fact]
    public void WrappingTheInnerCollectionInAClass_IsAccepted()
    {
        AssertDescribes(typeof(WrappedNestedCollectionConfiguration), "Rows");
    }

    [Fact]
    public void AStringIsNotACollectionOfCharacters()
    {
        // `string` implements `IEnumerable<char>`, so a naive check would
        // reject every `List<string>` in the tree.
        AssertDescribes(typeof(NestedCollectionRow), "Values");
    }

    /// <summary>
    ///   Asserts the generator got far enough through <paramref name="type"/>
    ///   to describe <paramref name="key"/> as a real element.
    /// </summary>
    /// <remarks>
    ///   The schema's <c>x-uiDefinition</c> bag is no longer a proxy for this:
    ///   it only carries what the validator reads, so a property with no
    ///   environment variable, no restart flag and no enum gets no bag at all.
    ///   The UI definition is where a described property now shows up.
    /// </remarks>
    /// <param name="type">The configuration type to generate.</param>
    /// <param name="key">The property key to look for.</param>
    private static void AssertDescribes(Type type, string key)
    {
        var wrapped = ShokoJsonSchemaGeneratorGoldenTests.CreateGenerator().GetSchemaForType(type);
        var definition = new UiDefinitionBuilder(NullLogger<UiDefinitionBuilder>.Instance).Build(Guid.Empty, type.Name, null, wrapped);
        var element = Flatten(definition.Root)
            .OfType<UiSectionContainerElement>()
            .Select(container => container.Items.GetValueOrDefault(key))
            .FirstOrDefault(x => x is not null);

        Assert.NotNull(element);
        Assert.IsNotType<UiUnknownElement>(element);
    }

    private static IEnumerable<UiElement> Flatten(UiElement element)
    {
        yield return element;
        switch (element)
        {
            case UiSectionContainerElement container:
                foreach (var item in container.Items.Values.SelectMany(Flatten))
                    yield return item;
                break;
            case UiListElement list:
                foreach (var item in Flatten(list.Item))
                    yield return item;
                break;
            case UiRecordElement record:
                foreach (var item in Flatten(record.KeyItem).Concat(Flatten(record.Item)))
                    yield return item;
                break;
        }
    }
}

/// <summary>
///   Groups every test that drives <see cref="ShokoJsonSchemaGenerator"/> into
///   one xUnit collection.
/// </summary>
/// <remarks>
///   Generating two schemas at once races inside NJsonSchema's and
///   Namotion.Reflection's shared XML-documentation caches and intermittently
///   drops descriptions. The server generates schemas serially under a lock, so
///   this only ever bites the test host.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public class ConfigurationSchemaCollection
{
    /// <summary>The collection's name.</summary>
    public const string Name = "ConfigurationSchema";
}

/// <summary>
///   Locates the repository directories the tests read from and write to.
/// </summary>
internal static class TestPaths
{
    /// <summary>
    ///   The repository root, found by walking up from the test assembly.
    /// </summary>
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    /// <summary>
    ///   Where the committed golden files live.
    /// </summary>
    public static string DataDirectory { get; } = Path.Combine(RepositoryRoot, "Shoko.Tests", "Data");

    /// <summary>
    ///   Where the proof-of-concept dumps are written.
    /// </summary>
    public static string OutputDirectory { get; } = Path.Combine(RepositoryRoot, "poc-output");

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Shoko.Server.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
