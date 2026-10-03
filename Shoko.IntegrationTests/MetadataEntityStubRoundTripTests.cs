using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Databases;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached.Metadata;
using Xunit;
using static Shoko.IntegrationTests.Sql;

namespace Shoko.IntegrationTests;

/// <summary>
/// Writes credits and links naming creators, characters, studios and networks
/// not stored yet into the migrated database, so the stubs they leave, a save
/// filling them in and the purge keeping the linked ones are read back from
/// each backend, and runs the steps that let a row be a stub again on it.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class MetadataEntityStubRoundTripTests(DatabaseMigrationFixture fixture)
{
    private static readonly MetadataSource _plugin = TestSources.Plugin;

    private static MetadataGuid ID(MetadataEntityType entityType, string id)
        => new(_plugin, entityType, id);

    /// <summary>
    /// Throws the caches away and reads the people, studios and networks
    /// again from the database.
    /// </summary>
    private void Reload()
    {
        var services = fixture.Services;
        foreach (var repository in new ICachedRepository[]
        {
            services.GetRequiredService<Metadata_CreatorRepository>(),
            services.GetRequiredService<Metadata_CharacterRepository>(),
            services.GetRequiredService<Metadata_StudioRepository>(),
            services.GetRequiredService<Metadata_Studio_EntryRepository>(),
            services.GetRequiredService<Metadata_NetworkRepository>(),
            services.GetRequiredService<Metadata_Network_EntryRepository>(),
        })
            repository.Populate(displayName: false);
    }

    [Fact]
    public void StubsReadBackFromTheDatabaseAndASaveFillsThemIn()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var people = fixture.Services.GetRequiredService<IMetadataPeopleStore>();
        var studios = fixture.Services.GetRequiredService<IMetadataStudioStore>();
        var creators = fixture.Services.GetRequiredService<Metadata_CreatorRepository>();
        var characters = fixture.Services.GetRequiredService<Metadata_CharacterRepository>();
        var studioRows = fixture.Services.GetRequiredService<Metadata_StudioRepository>();
        var networkRows = fixture.Services.GetRequiredService<Metadata_NetworkRepository>();
        var series = ID(MetadataEntityType.Series, "stub-series-1");
        var other = ID(MetadataEntityType.Series, "stub-series-2");
        var creator = ID(MetadataEntityType.Creator, "stub-creator-1");
        var character = ID(MetadataEntityType.Character, "stub-character-1");
        var studio = ID(MetadataEntityType.Studio, "stub-studio-1");
        var network = ID(MetadataEntityType.Network, "stub-network-1");
        var unlinked = ID(MetadataEntityType.Creator, "stub-creator-2");

        people.SetCast(series, [new() { CharacterID = character, CreatorID = creator, CreatorName = "Kana", Name = "Alice" }]);
        people.SetCrew(other, [new() { CreatorID = unlinked, Name = "Music" }]);
        studios.SetStudios(series, [new() { StudioID = studio, StudioName = "Sunrise" }]);
        studios.SetNetworks(series, [new MetadataEntryNetworkData { NetworkID = network, NetworkName = "Tokyo MX" }]);
        people.RemoveCrew(other);

        Reload();

        Assert.True(creators.GetByProviderID(_plugin, creator.ID)!.IsStub);
        Assert.Equal("Kana", creators.GetByProviderID(_plugin, creator.ID)!.Name);
        Assert.Equal("Alice", characters.GetByProviderID(_plugin, character.ID)!.Name);
        Assert.True(studioRows.GetByProviderID(_plugin, studio.ID)!.IsStub);
        Assert.Equal("Tokyo MX", networkRows.GetByProviderID(_plugin, network.ID)!.Name);
        Assert.Equal("Kana", people.GetCast(series).Single().Creator!.Name);

        // The purge keeps the stubs still named and drops the one let go.
        var purged = people.RemoveOrphaned(_plugin, DateTime.MaxValue);
        Assert.Contains(unlinked, purged);
        Assert.DoesNotContain(creator, purged);

        people.SaveCreators([new() { ID = creator, Name = "Kana Hanazawa" }]);
        studios.SaveStudios([new() { ID = studio, Name = "Sunrise" }]);
        Reload();
        Assert.False(creators.GetByProviderID(_plugin, creator.ID)!.IsStub);
        Assert.False(studioRows.GetByProviderID(_plugin, studio.ID)!.IsStub);
        Assert.True(characters.GetByProviderID(_plugin, character.ID)!.IsStub);

        people.RemoveCast(series);
        studios.RemoveStudios(series);
        studios.RemoveNetworks(series);
        people.RemoveOrphaned(_plugin, DateTime.MaxValue);
        studios.RemoveOrphaned(_plugin, DateTime.MaxValue);
    }

    [Fact]
    public void TheStubStepsKeepEveryRowAndLetARowBeAStub()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var people = fixture.Services.GetRequiredService<IMetadataPeopleStore>();
        var studios = fixture.Services.GetRequiredService<IMetadataStudioStore>();
        var creators = fixture.Services.GetRequiredService<Metadata_CreatorRepository>();
        var series = ID(MetadataEntityType.Series, "stub-series-3");
        var stub = ID(MetadataEntityType.Creator, "stub-creator-3");
        var full = ID(MetadataEntityType.Creator, "stub-creator-4");
        people.SaveCreators([new() { ID = full, Name = "Hiro", PlaceOfBirth = "Tokyo, Japan", IsRestricted = true }]);
        people.SetCrew(series, [new() { CreatorID = stub, CreatorName = "Mio", Name = "Music" }, new() { CreatorID = full, Name = "Director" }]);
        studios.SetStudios(series, [new() { StudioID = ID(MetadataEntityType.Studio, "stub-studio-3") }]);
        string[] tables = ["Metadata_Creator", "Metadata_Character", "Metadata_Studio"];

        // SQLite rebuilds each table from its own definition; the others change the column in place.
        using (var connection = fixture.OpenConnection())
        {
            var before = tables.Select(table => Read(connection, $"SELECT * FROM {table} ORDER BY {table}ID")).ToList();
            int[] revisions = fixture.Services.GetRequiredService<DatabaseFactory>().Instance is SQLite ? [205, 206, 207] : [206, 207, 208];
            foreach (var step in SchemaSteps.Get(fixture, revisions))
            {
                if (step.UpdateCommand is { } update)
                    Assert.True(update(connection).Item1);
                else
                    Execute(connection, step.Command!);
            }

            Assert.Equal(before, tables.Select(table => Read(connection, $"SELECT * FROM {table} ORDER BY {table}ID")).ToList());
            Assert.Equal(
                ["1"],
                Read(connection, $"SELECT COUNT(*) FROM Metadata_Creator WHERE ProviderID = '{stub.ID}' AND LastUpdatedAt IS NULL")
            );
        }

        Reload();
        Assert.True(creators.GetByProviderID(_plugin, stub.ID)!.IsStub);
        var kept = creators.GetAll().Max(creator => creator.Metadata_CreatorID);
        people.SetCrew(series, [new() { CreatorID = ID(MetadataEntityType.Creator, "stub-creator-5"), Name = "Music" }]);
        Assert.True(creators.GetByProviderID(_plugin, "stub-creator-5")!.Metadata_CreatorID > kept);

        people.RemoveCrew(series);
        studios.RemoveStudios(series);
        people.RemoveOrphaned(_plugin, DateTime.MaxValue);
        studios.RemoveOrphaned(_plugin, DateTime.MaxValue);
    }
}
