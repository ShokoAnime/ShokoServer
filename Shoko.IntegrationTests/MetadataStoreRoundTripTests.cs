using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Databases;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Xunit;
using static Shoko.IntegrationTests.Sql;

namespace Shoko.IntegrationTests;

/// <summary>
/// Writes through the typed metadata stores into the migrated database, then
/// reads every table back from it, so the mappings, the column types and the
/// unique indexes are checked on each backend.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class MetadataStoreRoundTripTests(DatabaseMigrationFixture fixture)
{
    private static readonly MetadataSource _plugin = TestSources.Plugin;

    private static readonly MetadataGuid _series = new(_plugin, MetadataEntityType.Series, "series-1");

    private static readonly MetadataGuid _other = new(_plugin, MetadataEntityType.Series, "series-2");

    private static readonly MetadataGuid _film = new(_plugin, MetadataEntityType.Movie, "film-1");

    private static MetadataGuid Creator(string id)
        => new(_plugin, MetadataEntityType.Creator, id);

    private static MetadataGuid Character(string id)
        => new(_plugin, MetadataEntityType.Character, id);

    private static MetadataGuid Tag(string id)
        => new(_plugin, MetadataEntityType.Tag, id);

    private static MetadataGuid Studio(string id)
        => new(_plugin, MetadataEntityType.Studio, id);

    private static MetadataGuid Network(string id)
        => new(_plugin, MetadataEntityType.Network, id);

    /// <summary>
    /// Throws the caches away and reads every store table again from the
    /// database.
    /// </summary>
    private void Reload()
    {
        var services = fixture.Services;
        foreach (var repository in new ICachedRepository[]
        {
            services.GetRequiredService<Metadata_CreatorRepository>(),
            services.GetRequiredService<Metadata_CharacterRepository>(),
            services.GetRequiredService<Metadata_CastRepository>(),
            services.GetRequiredService<Metadata_CrewRepository>(),
            services.GetRequiredService<Metadata_TagRepository>(),
            services.GetRequiredService<Metadata_Tag_EntryRepository>(),
            services.GetRequiredService<Metadata_StudioRepository>(),
            services.GetRequiredService<Metadata_Studio_EntryRepository>(),
            services.GetRequiredService<Metadata_NetworkRepository>(),
            services.GetRequiredService<Metadata_Network_EntryRepository>(),
            services.GetRequiredService<Metadata_RelationRepository>(),
            services.GetRequiredService<Metadata_SuggestionRepository>(),
            services.GetRequiredService<TextCache>(),
        })
            repository.Populate(displayName: false);
    }

    [Fact]
    public void WhatTheStoresWriteReadsBackFromTheDatabase()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var people = fixture.Services.GetRequiredService<IMetadataPeopleStore>();
        var tags = fixture.Services.GetRequiredService<IMetadataTagStore>();
        var studios = fixture.Services.GetRequiredService<IMetadataStudioStore>();
        var relations = fixture.Services.GetRequiredService<IMetadataRelationStore>();
        var suggestions = fixture.Services.GetRequiredService<IMetadataSuggestionStore>();
        var longText = new string('x', 6000);

        people.SaveCreators([
            new()
            {
                ID = Creator("c1"),
                Name = "Kana",
                Overview = longText,
                Gender = PersonGender.Female,
                BirthDay = new(1990, 4, 1),
                DeathDay = new(null, 11, 30),
                Resources = [new() { Type = ResourceType.Website, Name = "Homepage", Url = "https://example.com/kana", LanguageCode = "ja" }],
                AlternativeNames = [new() { Name = "かな", LanguageCode = "ja" }, new() { Name = "Kana-chan" }],
            },
            new() { ID = Creator("c2"), Name = "Hiro", Type = CreatorType.Company },
        ]);
        people.SaveCharacters([
            new()
            {
                ID = Character("x1"),
                Name = "Alice",
                OriginalName = "アリス",
                Gender = PersonGender.NonBinary,
                BirthDay = new(1999),
                AlternativeNames = [new() { Name = "Ally", LanguageCode = "en" }],
            },
        ]);
        people.SetCast(_series, [new() { CharacterID = Character("x1"), CreatorID = Creator("c1"), Name = "Alice", LanguageCode = "ja" }]);
        people.SetCrew(_series, [
            new() { CreatorID = Creator("c1"), Name = "Director", RoleType = CrewRoleType.Director },
            new() { CreatorID = Creator("c2"), Name = "Animation" },
        ]);
        // Swapping the crew and saving a creator again updates the rows in place.
        people.SetCast(_series, [
            new()
            {
                CharacterID = Character("x1"),
                CreatorID = Creator("c1"),
                Name = "Alice (young)",
                LanguageCode = "ja",
                RoleNotes = longText,
                DubGroup = "Studio Dub",
            },
        ]);
        people.SetCrew(_series, [
            new() { CreatorID = Creator("c2"), Name = "Animation" },
            new() { CreatorID = Creator("c1"), Name = "Director", RoleType = CrewRoleType.Director },
        ]);
        people.SaveCreators([new() { ID = Creator("c2"), Name = "Hiro Studio", Type = CreatorType.Company, BirthDay = new(1985, 7) }]);

        tags.SaveTags([
            new() { ID = Tag("t1"), Name = "Mecha", Category = "Theme" },
            new() { ID = Tag("t2"), Name = "Action", Kind = TagKind.Genre },
            new() { ID = Tag("t3"), Name = "giant robot", Kind = TagKind.Keyword },
        ]);
        tags.SetTags(_series, [new() { TagID = Tag("t1"), Weight = 80 }, new() { TagID = Tag("t2") }]);
        tags.SetTags(_series, [new() { TagID = Tag("t2") }, new() { TagID = Tag("t1"), Weight = 60, IsSpoiler = true }]);
        tags.SetTags(_other, [new() { TagID = Tag("t2") }]);

        studios.SaveStudios([new() { ID = Studio("s1"), Name = "Sunrise" }]);
        studios.SetStudios(_series, [new() { StudioID = Studio("s1") }, new() { StudioID = Studio("s1"), Type = StudioType.Production }]);
        studios.SaveNetworks([new() { ID = Network("n1"), Name = "Tokyo MX" }, new() { ID = Network("n2"), Name = "BS11" }]);
        studios.SetNetworks(_series, [Network("n2"), Network("n1")]);

        relations.SetRelations(_series, [
            new() { RelatedID = new(_plugin, MetadataEntityType.Series, "series-2"), RelationType = RelationType.Sequel },
            new() { RelatedID = new(_plugin, MetadataEntityType.Movie, "film-1"), RelationType = RelationType.SideStory },
        ]);
        relations.SetRelations(_series, [
            new() { RelatedID = new(_plugin, MetadataEntityType.Movie, "film-1"), RelationType = RelationType.SideStory },
            new() { RelatedID = new(_plugin, MetadataEntityType.Series, "series-2"), RelationType = RelationType.Sequel },
        ]);

        suggestions.SetSuggestions(_series, [
            new() { SuggestedID = new(_plugin, MetadataEntityType.Series, "series-2"), Order = 0, ApprovalRating = 87.5, Votes = 12, Score = -3 },
            new() { SuggestedID = new(_plugin, MetadataEntityType.Movie, "film-1"), Kind = SuggestionKind.Similar },
        ]);

        Reload();

        var creator = people.GetCreator(Creator("c1"));
        Assert.NotNull(creator);
        Assert.Equal(longText, creator.DefaultOverview?.Value);
        Assert.Equal(new FuzzyDateOnly(1990, 4, 1), creator.BirthDay);
        Assert.Equal(new FuzzyDateOnly(null, 11, 30), creator.DeathDay);
        Assert.Equal(PersonGender.Female, creator.Gender);
        var link = Assert.Single(((Metadata_Creator)creator).Resources);
        Assert.Equal((ResourceType.Website, "Homepage", "https://example.com/kana", "ja"), (link.Type, link.Name, link.Url, link.LanguageCode));
        Assert.Equal(["かな", "Kana-chan"], creator.AlternativeNames.Select(name => name.Value));
        Assert.Equal(["ja", "unk"], creator.AlternativeNames.Select(name => name.LanguageCode));
        Assert.All(creator.AlternativeNames, name => Assert.Equal(TitleType.Synonym, name.Type));
        var company = people.GetCreator(Creator("c2"));
        Assert.Equal("Hiro Studio", company?.Name);
        Assert.Equal(new FuzzyDateOnly(1985, 7), company?.BirthDay);
        Assert.Empty(((Metadata_Creator)company!).Resources);
        var character = people.GetCharacter(Character("x1"));
        Assert.Equal("アリス", character?.OriginalName);
        Assert.Equal(PersonGender.NonBinary, character?.Gender);
        Assert.Equal(new FuzzyDateOnly(1999), character?.BirthDay);
        Assert.Equal("Ally", Assert.Single(character!.AlternativeNames).Value);
        var cast = Assert.Single(people.GetCast(_series));
        Assert.Equal("Alice (young)", cast.Name);
        Assert.Equal(longText, cast.Description);
        Assert.Equal("Studio Dub", cast.DubGroup);
        Assert.Equal(creator.ID, cast.Creator?.ID);
        Assert.Equal(["Animation", "Director"], people.GetCrew(_series).Select(crew => crew.Name));
        Assert.Equal(CrewRoleType.Director, people.GetCrew(_series)[1].RoleType);

        Assert.Equal(["Action", "Mecha"], tags.GetTags(_series).Select(tag => tag.Name));
        Assert.Equal([null, 60], tags.GetTags(_series).Select(tag => tag.Weight));
        Assert.True(tags.GetTags(_series)[1].IsSpoiler);
        Assert.Equal("Theme", tags.GetTag(Tag("t1"))?.Category);
        Assert.Equal(TagKind.Keyword, tags.GetTag(Tag("t3"))?.Kind);
        Assert.Equal(["giant robot"], tags.GetAllTags(_plugin, TagKind.Keyword).Select(tag => tag.Name));
        Assert.Equal(2, tags.GetEntriesWithTag(Tag("t2")).Count);

        Assert.Equal([StudioType.Animation, StudioType.Production], studios.GetStudios(_series).Select(studio => studio.StudioType));
        Assert.Equal(["BS11", "Tokyo MX"], studios.GetNetworks(_series).Select(network => network.Name));
        Assert.Equal([_series], studios.GetEntriesForNetwork(Network("n1")));
        Assert.Null(fixture.Services.GetRequiredService<Metadata_StudioRepository>().GetByProviderID(_plugin, "s1")?.LastOrphanedAt);

        Assert.Equal(["film-1", "series-2"], relations.GetRelations<IMetadata, IMetadata>(_series).Select(relation => relation.RelatedID.ID));
        Assert.Equal(RelationType.Prequel, Assert.Single(relations.GetRelations<ISeries, ISeries>(_other)).RelationType);

        var suggestion = suggestions.GetSuggestions<ISeries, IMetadata>(_series)[0];
        Assert.Equal(87.5, suggestion.ApprovalRating);
        Assert.Equal(12, suggestion.Votes);
        Assert.Equal(-3, suggestion.Score);
        Assert.Equal(SuggestionKind.Similar, Assert.Single(suggestions.GetSuggestedBy<ISeries, IMovie>(_film)).Kind);

        Assert.Null(fixture.Services.GetRequiredService<Metadata_CreatorRepository>().GetByProviderID(_plugin, "c1")?.LastOrphanedAt);
        Assert.Equal(1, people.RemoveCast(_series));
        Assert.Equal(2, people.RemoveCrew(_series));

        // The people the removals left uncredited are stamped, in the database too.
        Reload();
        var orphanedAt = fixture.Services.GetRequiredService<Metadata_CreatorRepository>().GetByProviderID(_plugin, "c1")?.LastOrphanedAt;
        Assert.NotNull(orphanedAt);
        Assert.InRange(orphanedAt.Value, DateTime.Now.AddMinutes(-5), DateTime.Now.AddMinutes(1));
        Assert.Empty(people.RemoveOrphaned(_plugin, DateTime.Now.AddDays(-1)));
        Assert.Equal(3, people.RemoveOrphaned(_plugin, DateTime.MaxValue).Count);
        Assert.Equal(2, tags.RemoveTags(_series));
        Assert.Equal(2, studios.RemoveStudios(_series));
        Assert.Equal(2, studios.RemoveNetworks(_series));
        Assert.Equal(2, relations.RemoveRelations(_series));
        Assert.Equal(2, suggestions.RemoveSuggestions(_series));

        Reload();

        Assert.Empty(people.GetCast(_series));
        Assert.Null(people.GetCreator(Creator("c1")));
        Assert.Empty(fixture.Services.GetRequiredService<TextCache>().GetRows(Creator("c1")));
        Assert.Empty(tags.GetTags(_series));
        Assert.Single(tags.GetTags(_other));
        Assert.Empty(studios.GetStudios(_series));
        Assert.Empty(studios.GetNetworks(_series));
        Assert.NotNull(fixture.Services.GetRequiredService<Metadata_StudioRepository>().GetByProviderID(_plugin, "s1")?.LastOrphanedAt);
        Assert.NotNull(fixture.Services.GetRequiredService<Metadata_NetworkRepository>().GetByProviderID(_plugin, "n1")?.LastOrphanedAt);

        // The studio and networks nothing names any more go with the purge.
        Assert.Equal(
            [Studio("s1"), Network("n1"), Network("n2")],
            studios.RemoveOrphaned(_plugin, DateTime.MaxValue).OrderBy(id => id.EntityType).ThenBy(id => id.ID)
        );
        Reload();
        Assert.Null(studios.GetStudio(Studio("s1")));
        Assert.Null(studios.GetNetwork(Network("n1")));
        Assert.Empty(relations.GetRelations<ISeries, ISeries>(_other));
        Assert.Empty(suggestions.GetSuggestedBy<ISeries, IMovie>(_film));
    }

    [Fact]
    public void FuzzyBirthdaysReadBackFromTheDatabase()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var people = fixture.Services.GetRequiredService<IMetadataPeopleStore>();
        var creators = fixture.Services.GetRequiredService<Metadata_CreatorRepository>();
        var source = MetadataNumberRegistry.GetNumber(_plugin);

        people.SaveCreators([
            new() { ID = Creator("fz-leap"), Name = "Leap", BirthDay = new(null, 2, 29) },
            new() { ID = Creator("fz-month"), Name = "Month", BirthDay = new(null, 12) },
            new() { ID = Creator("fz-dated"), Name = "Dated", BirthDay = new PartialDateOnly(1990, 4) },
        ]);
        people.SaveCharacters([new() { ID = Character("fz-yearless"), Name = "Yearless", BirthDay = new(null, 7, 4) }]);

        using var connection = fixture.OpenConnection();
        try
        {
            // A row written before a year could be missing reads back as it was.
            Execute(connection, "INSERT INTO Metadata_Creator (Source, ProviderID, Name, Type, BirthDay, LastUpdatedAt) " +
                $"VALUES ({source}, 'fz-stored', 'Stored', 0, '1985-07-15', '2026-01-01 00:00:00')");

            Reload();

            Assert.Equal(
                ["1990-04", "--02-29", "--12"],
                Read(connection, "SELECT BirthDay FROM Metadata_Creator WHERE ProviderID IN ('fz-leap', 'fz-month', 'fz-dated') ORDER BY ProviderID")
            );
            Assert.Equal(new FuzzyDateOnly(null, 2, 29), people.GetCreator(Creator("fz-leap"))?.BirthDay);
            Assert.Equal(new FuzzyDateOnly(null, 12), people.GetCreator(Creator("fz-month"))?.BirthDay);
            Assert.Equal(new FuzzyDateOnly(1990, 4), people.GetCreator(Creator("fz-dated"))?.BirthDay);
            Assert.Equal(new FuzzyDateOnly(1985, 7, 15), people.GetCreator(Creator("fz-stored"))?.BirthDay);
            Assert.Equal(new FuzzyDateOnly(null, 7, 4), people.GetCharacter(Character("fz-yearless"))?.BirthDay);
        }
        finally
        {
            Execute(connection, "DELETE FROM Metadata_Creator WHERE ProviderID LIKE 'fz-%'");
            Execute(connection, "DELETE FROM Metadata_Character WHERE ProviderID LIKE 'fz-%'");
            creators.Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);
            fixture.Services.GetRequiredService<Metadata_CharacterRepository>()
                .Populate(displayName: false, cancellationToken: TestContext.Current.CancellationToken);
        }
    }
}
