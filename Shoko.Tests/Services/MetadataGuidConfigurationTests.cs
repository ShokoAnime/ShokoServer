using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Components;
using Shoko.Abstractions.UI.Elements;
using Shoko.Abstractions.UI.Enums;
using Shoko.Server.Services.Configuration;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   Coverage for <see cref="MetadataGuid"/> inside a configuration: as a
///   dictionary key and as a list entry.
/// </summary>
[Collection(ConfigurationSchemaCollection.Name)]
public class MetadataGuidConfigurationTests
{
    [Fact]
    public void AMetadataGuidKeysARecordAndRoundTrips()
    {
        var wrapped = TestSchemaGenerator.Create().GetSchemaForType(typeof(GuidConfiguration));
        var definition = new UiDefinitionBuilder(NullLogger<UiDefinitionBuilder>.Instance).Build(Guid.Empty, "Guid", null, wrapped, listsOptions: true);
        var root = Assert.IsType<UiSectionContainerElement>(definition.Root);
        var record = Assert.IsType<UiRecordElement>(root.Items[nameof(GuidConfiguration.ByEntry)]);
        var list = Assert.IsType<UiListElement>(root.Items[nameof(GuidConfiguration.Excluded)]);
        Assert.True(Assert.IsType<UiStringElement>(record.KeyItem).HasOptions);
        Assert.IsType<UiStringElement>(list.Item);

        var configuration = new GuidConfiguration
        {
            ByEntry = { [MetadataGuid.Parse("anidb://release-group/1")] = 2, [MetadataGuid.Parse("offline://release-group/Some Group")] = 3 },
            Excluded = [MetadataGuid.Parse("anidb://release-group/2")],
        };
        var settings = ShokoJsonSerializers.CreateNewtonsoftSettings();
        var json = JsonConvert.SerializeObject(configuration, settings);
        Assert.Empty(wrapped.Schema.Validate(json));

        var read = JsonConvert.DeserializeObject<GuidConfiguration>(json, settings)!;
        Assert.Equal(configuration.ByEntry, read.ByEntry);
        Assert.Equal(configuration.Excluded, read.Excluded);
    }

    /// <summary>A configuration keyed and listing by metadata identifiers.</summary>
    public class GuidConfiguration : INewtonsoftJsonConfiguration
    {
        /// <summary>Values keyed by entry.</summary>
        public Dictionary<MetadataGuid, int> ByEntry { get; set; } = [];

        /// <summary>A list of entries.</summary>
        public List<MetadataGuid> Excluded { get; set; } = [];

        /// <summary>Lists the keys, labelled.</summary>
        [OptionsProvider(nameof(ByEntry), Target = OptionsTarget.Keys)]
        public SelectOption<MetadataGuid>[] ListKeys()
            => [new(MetadataGuid.Parse("anidb://release-group/1"), "One")];
    }
}
