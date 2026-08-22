using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.UI;
using Shoko.Abstractions.UI.Elements;
using Shoko.Server.Services.Configuration;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   Generates a schema and a UI definition for every configuration the server
///   ships, so a change to the generator cannot break one of them at startup.
/// </summary>
/// <remarks>
///   The unit tests above drive `ServerSettings` and a handful of fixtures; the
///   provider and renamer configurations only ever get exercised here.
/// </remarks>
[Collection(ConfigurationSchemaCollection.Name)]
public class AllConfigurationTypesTests
{
    public static TheoryData<Type> ConfigurationTypes
    {
        get
        {
            var data = new TheoryData<Type>();
            foreach (var type in typeof(ServerSettings).Assembly.GetTypes()
                .Where(x => x is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false } && x.IsAssignableTo(typeof(IConfiguration)))
                .Where(x => x.GetConstructor(Type.EmptyTypes) is not null)
                .OrderBy(x => x.FullName, StringComparer.Ordinal))
                data.Add(type);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ConfigurationTypes))]
    public void EveryConfiguration_ProducesASchemaAndADefinition(Type type)
    {
        var wrapped = ShokoJsonSchemaGeneratorGoldenTests.CreateGenerator().GetSchemaForType(type);
        var definition = new UiDefinitionBuilder(NullLogger<UiDefinitionBuilder>.Instance)
            .Build(Guid.Empty, wrapped.Schema.Title ?? type.Name, null, wrapped);

        Assert.NotNull(definition.Root);
        Assert.All(Flatten(definition.Root), x => Assert.NotEqual(UiElementKind.Unknown, x.Kind));
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
