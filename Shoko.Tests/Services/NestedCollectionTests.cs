using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Shoko.Abstractions.UI;
using Shoko.Abstractions.UI.Elements;
using Shoko.Server.Services.Configuration;
using Xunit;

namespace Shoko.Tests.Services;

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
        var generator = TestSchemaGenerator.Create();
        var exception = Assert.Throws<NotSupportedException>(() => generator.GetSchemaForType(type));

        Assert.Contains($"\"{type.Name}.Values\"", exception.Message, StringComparison.Ordinal);
        Assert.Contains(expectedTypeName, exception.Message, StringComparison.Ordinal);
        Assert.Contains("Wrap the inner collection in a class", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///   Shapes that look like a nested collection but are not one: the
    ///   generator must still produce a usable schema for each.
    /// </summary>
    /// <param name="configurationType">The configuration type to generate.</param>
    /// <param name="key">The property key that must be described.</param>
    [Theory]
    // The two levels get distinct keys ("+Dict" and "+List"), so nothing collides; a dictionary of scalar arrays is an ordinary shape.
    [InlineData(typeof(NestedRecordOfListConfiguration), "Values")]
    [InlineData(typeof(WrappedNestedCollectionConfiguration), "Rows")]
    // `string` implements `IEnumerable<char>`, so a naive check would reject every `List<string>` in the tree.
    [InlineData(typeof(NestedCollectionRow), "Values")]
    public void AShapeThatIsNotANestedCollection_IsAccepted(Type configurationType, string key)
        => AssertDescribes(configurationType, key);

    /// <summary>
    ///   Asserts the generator got far enough through <paramref name="type"/>
    ///   to describe <paramref name="key"/> as a real element.
    /// </summary>
    /// <remarks>
    ///   The schema's <c>x-uiDefinition</c> bag is no proxy for this: it only
    ///   carries what the validator reads, so a property with no environment
    ///   variable, no restart flag and no enum gets no bag at all. The UI
    ///   definition is where a described property shows up.
    /// </remarks>
    /// <param name="type">The configuration type to generate.</param>
    /// <param name="key">The property key to look for.</param>
    private static void AssertDescribes(Type type, string key)
    {
        var wrapped = TestSchemaGenerator.Create().GetSchemaForType(type);
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
