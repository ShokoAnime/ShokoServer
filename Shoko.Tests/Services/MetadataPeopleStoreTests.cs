using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Repositories.Direct.Metadata;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers <see cref="MetadataPeopleStore"/> against in-memory tables: credits
/// keep the order they were given, a new set replaces the old one and says
/// how much changed, a credit refused changes nothing, a
/// person's alternative names live in the text table, and people no credit
/// names are stamped as orphaned and removed after a cutoff.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class MetadataPeopleStoreTests
{
    #region Helpers

    private static readonly MetadataGuid _series = new(TestSources.AniList, MetadataEntityType.Series, "1");

    private static readonly MetadataGuid _otherSeries = new(TestSources.AniList, MetadataEntityType.Series, "2");

    private sealed class Tables
    {
        public Metadata_CreatorRepository Creators { get; }
            = CachedRepo.Build<Metadata_CreatorRepository, int, Metadata_Creator>(row => row.Metadata_CreatorID);

        public Metadata_CharacterRepository Characters { get; }
            = CachedRepo.Build<Metadata_CharacterRepository, int, Metadata_Character>(row => row.Metadata_CharacterID);

        public Metadata_CastRepository Cast { get; } = new InMemoryCastRepository();

        public Metadata_CrewRepository Crew { get; } = new InMemoryCrewRepository();

        public TextCache Texts { get; } = new();

        public CacheOnlyRowWriter Writer { get; } = new();

        public MetadataTextStore TextStore { get; }

        public MetadataTextManager TextManager { get; }

        public Tables()
        {
            TextStore = new(Texts, Writer);
            TextManager = TestTextManager.Build(TextStore);
        }

        public MetadataPeopleStore Store() => new(Creators, Characters, Cast, Crew, TextStore, Writer);
    }

    private static Tables Seeded()
    {
        var tables = new Tables();
        var store = tables.Store();
        store.SaveCreators([Creator("c1", "Kana"), Creator("c2", "Hiro"), Creator("c3", "Mio")]);
        store.SaveCharacters([Character("x1", "Alice"), Character("x2", "Bob")]);
        return tables;
    }

    private static MetadataGuid CreatorID(string id, MetadataSource? source = null)
        => new(source ?? TestSources.AniList, MetadataEntityType.Creator, id);

    private static MetadataGuid CharacterID(string id, MetadataSource? source = null)
        => new(source ?? TestSources.AniList, MetadataEntityType.Character, id);

    private static MetadataCreatorData Creator(string id, string name, MetadataSource? source = null)
        => new() { ID = CreatorID(id, source), Name = name };

    private static MetadataCharacterData Character(string id, string name, MetadataSource? source = null)
        => new() { ID = CharacterID(id, source), Name = name };

    private static MetadataCastData Role(string character, string? creator, string? languageCode = "ja", string? name = null)
        => new()
        {
            CharacterID = CharacterID(character),
            CreatorID = creator is null ? null : CreatorID(creator),
            Name = name ?? character,
            LanguageCode = languageCode,
        };

    private static MetadataCrewData Job(string creator, string name, string? languageCode = null)
        => new() { CreatorID = CreatorID(creator), Name = name, LanguageCode = languageCode };

    #endregion

    #region Credits

    [Fact]
    public void CreditsReadBackInTheOrderGiven()
    {
        var tables = Seeded();
        var store = tables.Store();
        // A credit names its creator and character by their stored rows.
        using var scope = new RepoFactoryScope().Set(tables.Creators).Set(tables.Characters);

        store.SetCast(_series, [Role("x2", "c2"), Role("x1", "c1"), Role("x1", "c3", "en")]);
        store.SetCrew(_series, [Job("c3", "Music"), Job("c1", "Director")]);

        Assert.Equal(["x2", "x1", "x1"], store.GetCast(_series).Select(cast => cast.Name));
        Assert.Equal(["ja", "ja", "en"], store.GetCast(_series).Select(cast => cast.LanguageCode));
        Assert.Equal(["Music", "Director"], store.GetCrew(_series).Select(crew => crew.Name));
        Assert.Equal(CreatorID("c2"), store.GetCast(_series)[0].CreatorID);
    }

    [Fact]
    public void SettingTheCastAgainReplacesItAndKeepsTheCreditsThatStay()
    {
        var tables = Seeded();
        var store = tables.Store();
        store.SetCast(_series, [Role("x1", "c1"), Role("x2", "c2")]);
        store.SetCrew(_series, [Job("c3", "Music")]);
        var kept = ((Metadata_Cast)store.GetCast(_series).Single(cast => cast.Name is "x2")).Metadata_CastID;

        store.SetCast(_series, [Role("x2", "c2")]);

        var cast = Assert.Single(store.GetCast(_series));
        Assert.Equal(kept, ((Metadata_Cast)cast).Metadata_CastID);
        Assert.Equal(0, ((Metadata_Cast)cast).Ordering);
        Assert.Single(tables.Cast.GetAll());
        // The crew is its own list, and setting the cast leaves it alone.
        Assert.Single(store.GetCrew(_series));
    }

    [Fact]
    public void SettingCreditsSaysHowManyWereAddedChangedOrRemoved()
    {
        var tables = Seeded();
        var store = tables.Store();

        Assert.Equal(2, store.SetCast(_series, [Role("x1", "c1"), Role("x2", "c2")]));
        var rows = tables.Cast.GetAll().OrderBy(row => row.Metadata_CastID).ToList();
        Assert.Equal(0, store.SetCast(_series, [Role("x1", "c1"), Role("x2", "c2")]));

        // The cached rows are still the very ones the first write stored.
        Assert.Equal(rows, tables.Cast.GetAll().OrderBy(row => row.Metadata_CastID), ReferenceEqualityComparer.Instance);
        Assert.Equal(1, store.SetCast(_series, [Role("x1", "c1"), Role("x2", "c2", name: "Bobby")]));
        // Swapping the two moves both.
        Assert.Equal(2, store.SetCast(_series, [Role("x2", "c2", name: "Bobby"), Role("x1", "c1")]));
        Assert.Equal(2, store.RemoveCast(_series));
        Assert.Equal(0, store.RemoveCast(_series));

        Assert.Equal(1, store.SetCrew(_series, [Job("c1", "Director")]));
        Assert.Equal(0, store.SetCrew(_series, [Job("c1", "Director")]));
        Assert.Equal(1, store.SetCrew(_series, []));
    }

    [Fact]
    public void ACrewCreditIsKnownByItsCreatorAndJob()
    {
        var tables = Seeded();
        var store = tables.Store();
        store.SetCrew(_series, [Job("c1", "Translation", "en")]);
        var id = tables.Crew.GetAll().Single().Metadata_CrewID;

        Assert.Equal(1, store.SetCrew(_series, [Job("c1", "Translation", "de")]));

        var crew = tables.Crew.GetAll().Single();
        Assert.Equal(id, crew.Metadata_CrewID);
        Assert.Equal("de", crew.LanguageCode);
    }

    [Fact]
    public void ACastCreditKeepsItsNotesAndDubGroup()
    {
        var tables = Seeded();
        var store = tables.Store();

        store.SetCast(_series, [Role("x1", "c1", "en") with { RoleNotes = "Child", DubGroup = "Studio Dub" }]);
        var id = tables.Cast.GetAll().Single().Metadata_CastID;
        Assert.Equal(1, store.SetCast(_series, [Role("x1", "c1", "en") with { RoleNotes = "Adult", DubGroup = " " }]));

        var cast = Assert.Single(store.GetCast(_series));
        Assert.Equal(id, ((Metadata_Cast)cast).Metadata_CastID);
        Assert.Equal("Adult", cast.Description);
        Assert.Null(cast.DubGroup);
    }

    [Fact]
    public void ACreditRefusedChangesNothing()
    {
        var tables = Seeded();
        var store = tables.Store();
        store.SetCast(_series, [Role("x1", "c1")]);
        var before = tables.Writer.Writes;

        // The second credit names a character as its creator, so neither is written.
        Assert.Throws<ArgumentException>(() => store.SetCast(_series, [Role("x9", "c9"), Role("x8", null) with { CreatorID = CharacterID("x1") }]));
        Assert.Throws<ArgumentException>(() => store.SetCrew(_series, [Job("c9", "Music"), Job("c1", "Music") with { LanguageCode = new string('x', 33) }]));

        Assert.Equal(before, tables.Writer.Writes);
        Assert.Null(tables.Creators.GetByProviderID(TestSources.AniList, "c9"));
        Assert.Null(tables.Characters.GetByProviderID(TestSources.AniList, "x9"));
        Assert.Equal(["x1"], store.GetCast(_series).Select(cast => cast.Name));
    }

    [Fact]
    public void ACreditOnlyFindsPeopleOnItsOwnSource()
    {
        var tables = Seeded();
        var store = tables.Store();
        store.SaveCreators([Creator("t1", "Elsewhere", TestSources.Plugin)]);

        Assert.Throws<ArgumentException>(() => store.SetCrew(_series, [Job("c1", "Music") with { CreatorID = CreatorID("t1", TestSources.Plugin) }]));

        // The same ID on the credit's own source is another creator, kept as a stub.
        store.SetCrew(_series, [Job("t1", "Music")]);
        Assert.True(tables.Creators.GetByProviderID(TestSources.AniList, "t1")!.IsStub);
        Assert.False(tables.Creators.GetByProviderID(TestSources.Plugin, "t1")!.IsStub);
    }

    [Fact]
    public void AFailedWriteLeavesTheCachedCreditsAsTheyWere()
    {
        var tables = Seeded();
        var store = tables.Store();
        store.SetCast(_series, [Role("x1", "c1"), Role("x2", "c2")]);
        tables.Writer.Fail = true;

        Assert.Throws<InvalidOperationException>(() => store.SetCast(_series, [Role("x2", "c3", "en"), Role("x1", "c1")]));

        Assert.Equal(["x1", "x2"], store.GetCast(_series).Select(cast => cast.Name));
        Assert.Equal([0, 1], store.GetCast(_series).Select(cast => ((Metadata_Cast)cast).Ordering));
    }

    #endregion

    #region People

    [Fact]
    public void SavingAPersonAgainUpdatesTheSameRowAndTheLastCopyWins()
    {
        var tables = Seeded();
        var store = tables.Store();
        var id = ((Metadata_Creator)store.GetCreator(CreatorID("c1"))!).Metadata_CreatorID;

        store.SaveCreators([Creator("c1", "First"), Creator("c1", "Second")]);

        var creator = store.GetCreator(CreatorID("c1"));
        Assert.Equal(id, ((Metadata_Creator)creator!).Metadata_CreatorID);
        Assert.Equal("Second", creator.Name);
        Assert.Equal(3, tables.Creators.GetAll().Count);
    }

    [Fact]
    public void APersonKeepsItsGenderBirthdayAndLinks()
    {
        var tables = Seeded();
        var store = tables.Store();
        var link = new Resource { Type = ResourceType.Website, Name = "Homepage", Url = "https://example.com/kana" };

        store.SaveCreators([
            Creator("c1", "Kana") with { Gender = PersonGender.Female, BirthDay = new(1990, 4), DeathDay = new(2070), Resources = [link] },
            Creator("c2", "Yui") with { BirthDay = new(null, 2, 29), DeathDay = default(FuzzyDateOnly) },
        ]);
        store.SaveCharacters([Character("x1", "Alice") with { Gender = PersonGender.NonBinary, BirthDay = new(2000, 12, 24) }]);

        var creator = (Metadata_Creator)store.GetCreator(CreatorID("c1"))!;
        Assert.Equal(PersonGender.Female, ((ICreator)creator).Gender);
        Assert.Equal(new FuzzyDateOnly(1990, 4), ((ICreator)creator).BirthDay);
        Assert.Equal(new FuzzyDateOnly(2070), ((ICreator)creator).DeathDay);
        Assert.Equal("https://example.com/kana", Assert.Single(creator.Resources).Url);
        var character = (Metadata_Character)store.GetCharacter(CharacterID("x1"))!;
        Assert.Equal(PersonGender.NonBinary, ((ICharacter)character).Gender);
        Assert.Equal(new FuzzyDateOnly(2000, 12, 24), ((ICharacter)character).BirthDay);
        Assert.Empty(character.Resources);

        // A date without a year is kept, and one with no parts is none.
        Assert.Equal(new FuzzyDateOnly(null, 2, 29), store.GetCreator(CreatorID("c2"))!.BirthDay);
        Assert.Null(store.GetCreator(CreatorID("c2"))!.DeathDay);
    }

    [Fact]
    public void APersonsAlternativeNamesAreSynonymTitlesOnTheTextTable()
    {
        var tables = Seeded();
        var store = tables.Store();
        using var scope = new RepoFactoryScope().Set(tables.Texts).Set(tables.TextManager);

        store.SaveCreators([Creator("c1", "Kana") with { AlternativeNames = [new() { Name = "かな", LanguageCode = "ja" }, new() { Name = "Kana-chan" }] }]);
        store.SaveCharacters([Character("x1", "Alice") with { AlternativeNames = [new() { Name = "Ally", LanguageCode = "en" }] }]);

        var names = ((ICreator)store.GetCreator(CreatorID("c1"))!).AlternativeNames;
        Assert.Equal(["かな", "Kana-chan"], names.Select(name => name.Value));
        Assert.Equal(["ja", "unk"], names.Select(name => name.LanguageCode));
        Assert.Equal([TitleLanguage.Japanese, TitleLanguage.Unknown], names.Select(name => name.Language));
        Assert.All(names, name => Assert.Equal(TitleType.Synonym, name.Type));
        Assert.All(names, name => Assert.Equal(TestSources.AniList, name.Source));
        Assert.Equal("Ally", Assert.Single(((ICharacter)store.GetCharacter(CharacterID("x1"))!).AlternativeNames).Value);

        // Saving the creator again replaces its names, and leaves the character's alone.
        store.SaveCreators([Creator("c1", "Kana") with { AlternativeNames = [new() { Name = "Kana-chan" }] }]);

        Assert.Equal(["Kana-chan"], ((ICreator)store.GetCreator(CreatorID("c1"))!).AlternativeNames.Select(name => name.Value));
        Assert.Equal(2, tables.Texts.GetAll().Count);
    }

    [Fact]
    public void AnOverlongLanguageCodeOnANameSavesNothing()
    {
        var tables = Seeded();
        var store = tables.Store();
        var before = tables.Writer.Writes;

        Assert.Throws<ArgumentException>(() => store.SaveCreators([
            Creator("c9", "New") with { AlternativeNames = [new() { Name = "Too long", LanguageCode = new string('x', 33) }] },
        ]));

        Assert.Equal(before, tables.Writer.Writes);
        Assert.Null(store.GetCreator(CreatorID("c9")));
    }

    [Fact]
    public void APersonNothingCreditsIsStampedAndCreditingItClearsTheStamp()
    {
        var tables = Seeded();
        var store = tables.Store();
        Metadata_Creator StoredCreator(string id) => tables.Creators.GetByProviderID(TestSources.AniList, id)!;
        Metadata_Character StoredCharacter(string id) => tables.Characters.GetByProviderID(TestSources.AniList, id)!;

        // Saved without a credit, everyone starts out orphaned.
        Assert.NotNull(StoredCreator("c1").LastOrphanedAt);
        Assert.NotNull(StoredCharacter("x1").LastOrphanedAt);

        store.SetCast(_series, [Role("x1", "c1")]);
        store.SetCrew(_series, [Job("c2", "Director")]);

        Assert.Null(StoredCreator("c1").LastOrphanedAt);
        Assert.Null(StoredCreator("c2").LastOrphanedAt);
        Assert.Null(StoredCharacter("x1").LastOrphanedAt);
        Assert.NotNull(StoredCreator("c3").LastOrphanedAt);

        // Saving a credited person again keeps it unstamped.
        store.SaveCreators([Creator("c1", "Kana")]);
        Assert.Null(StoredCreator("c1").LastOrphanedAt);

        // Losing the cast credit orphans c1 and x1, but c2 keeps its crew credit.
        store.RemoveCast(_series);

        Assert.NotNull(StoredCreator("c1").LastOrphanedAt);
        Assert.NotNull(StoredCharacter("x1").LastOrphanedAt);
        Assert.Null(StoredCreator("c2").LastOrphanedAt);

        // A crew credit on another entry clears c1's stamp again.
        store.SetCrew(_otherSeries, [Job("c1", "Music")]);

        Assert.Null(StoredCreator("c1").LastOrphanedAt);
    }

    [Fact]
    public void RemovingTheOrphanedTakesOnlyThoseOrphanedBeforeTheCutoffAndTheirNames()
    {
        var tables = Seeded();
        var store = tables.Store();
        store.SaveCreators([
            Creator("t1", "Elsewhere", TestSources.Plugin),
            Creator("c4", "Old") with { AlternativeNames = [new() { Name = "Oldie" }] },
        ]);
        store.SetCast(_series, [Role("x1", "c1")]);
        store.SetCrew(_series, [Job("c2", "Director")]);
        var longAgo = DateTime.Now.AddDays(-10);
        tables.Creators.GetByProviderID(TestSources.AniList, "c4")!.LastOrphanedAt = longAgo;
        tables.Creators.GetByProviderID(TestSources.Plugin, "t1")!.LastOrphanedAt = longAgo;
        tables.Characters.GetByProviderID(TestSources.AniList, "x2")!.LastOrphanedAt = longAgo;

        // One stored before stamps were kept has none, and is stamped instead.
        tables.Creators.GetByProviderID(TestSources.AniList, "c3")!.LastOrphanedAt = null;

        var removed = store.RemoveOrphaned(TestSources.AniList, DateTime.Now.AddDays(-7));

        Assert.Equal([CreatorID("c4"), CharacterID("x2")], removed);
        Assert.Null(store.GetCreator(CreatorID("c4")));
        Assert.Null(store.GetCharacter(CharacterID("x2")));
        Assert.NotNull(store.GetCreator(CreatorID("c1")));
        Assert.NotNull(store.GetCreator(CreatorID("c2")));
        Assert.NotNull(store.GetCharacter(CharacterID("x1")));
        Assert.NotNull(store.GetCreator(CreatorID("t1", TestSources.Plugin)));
        Assert.NotNull(tables.Creators.GetByProviderID(TestSources.AniList, "c3")!.LastOrphanedAt);
        Assert.Empty(tables.Texts.GetAll());
        Assert.Throws<ArgumentException>(() => store.RemoveOrphaned(MetadataSource.AniDB, DateTime.Now));
    }

    [Fact]
    public void AnIDNamingAnotherKindIsRefused()
    {
        var tables = Seeded();
        var store = tables.Store();

        Assert.Throws<ArgumentException>(() => store.SaveCreators([new() { ID = CharacterID("c9"), Name = "Nobody" }]));
        Assert.Throws<ArgumentException>(() => store.SaveCharacters([new() { ID = CreatorID("x9"), Name = "Nobody" }]));
        Assert.Throws<ArgumentException>(() => store.SetCast(_series, [new() { CharacterID = CreatorID("c1"), Name = "Nobody" }]));
        Assert.Null(store.GetCreator(CharacterID("c1")));
        Assert.Null(store.GetCharacter(CreatorID("x1")));
        Assert.Equal(3, tables.Creators.GetAll().Count);
    }

    #endregion
}
