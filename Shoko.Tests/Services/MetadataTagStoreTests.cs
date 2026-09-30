using System;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers <see cref="MetadataTagStore"/> against in-memory tables. A tag read through an entry carries that entry's
/// weight and spoiler flag, which it looks up from the tag table through
/// <c>RepoFactory</c>, so these tests share its collection.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class MetadataTagStoreTests
{
    private static readonly MetadataGuid _series = new(TestSources.AniList, MetadataEntityType.Series, "1");

    private static readonly MetadataGuid _movie = new(TestSources.AniList, MetadataEntityType.Movie, "5");

    private static MetadataGuid TagID(string id)
        => new(TestSources.AniList, MetadataEntityType.Tag, id);

    private static MetadataGuid StudioID(string id)
        => new(TestSources.AniList, MetadataEntityType.Studio, id);

    private static MetadataTagData Tag(string id, string name, TagKind kind = TagKind.Tag, bool isSpoiler = false)
        => new() { ID = TagID(id), Name = name, Kind = kind, IsSpoiler = isSpoiler };

    private static MetadataEntryTagData On(string id, int? weight = null, bool isSpoiler = false)
        => new() { TagID = TagID(id), Weight = weight, IsSpoiler = isSpoiler };

    private static (MetadataTagStore Store, Metadata_TagRepository Tags, Metadata_Tag_EntryRepository Entries, CacheOnlyRowWriter Writer) TagStore()
    {
        var tags = CachedRepo.Build<Metadata_TagRepository, int, Metadata_Tag>(row => row.Metadata_TagID);
        var entries = CachedRepo.Build<Metadata_Tag_EntryRepository, int, Metadata_Tag_Entry>(row => row.Metadata_Tag_EntryID);
        var writer = new CacheOnlyRowWriter();
        var store = new MetadataTagStore(tags, entries, writer, new MetadataTextStore(new TextCache(), writer));
        store.SaveTags([Tag("1", "Mecha"), Tag("2", "Action", TagKind.Genre), Tag("3", "Twist", isSpoiler: true), Tag("4", "Drama", TagKind.Genre)]);
        return (store, tags, entries, writer);
    }

    [Fact]
    public void AnEntrysTagsReadBackInOrderWithTheirOwnWeightAndSpoilerFlag()
    {
        var (store, tags, _, _) = TagStore();
        using var scope = new RepoFactoryScope().Set(tags);

        store.SetTags(_series, [On("3", 40), On("1", 90, isSpoiler: true), On("2"), On("1", 10)]);

        var read = store.GetTags(_series);
        Assert.Equal(["Twist", "Mecha", "Action"], read.Select(tag => tag.Name));
        // The first 'Mecha' wins over the repeat.
        Assert.Equal([40, 90, null], read.Select(tag => tag.Weight));
        // Twist spoils everywhere, Mecha only here, Action nowhere.
        Assert.Equal([true, true, false], read.Select(tag => tag.IsSpoiler));
        Assert.Equal(new MetadataGuid(TestSources.AniList, MetadataEntityType.Tag, "1"), read[1].ID);
        Assert.Equal(TagKind.Genre, read[2].Kind);
    }

    [Fact]
    public void SettingTagsAgainReplacesThem()
    {
        var (store, tags, entries, _) = TagStore();
        int IDOf(string providerID) => tags.GetByProviderID(TestSources.AniList, providerID)!.Metadata_TagID;
        store.SetTags(_series, [On("1"), On("2"), On("3")]);
        var kept = entries.GetByEntry(_series).Single(row => row.TagID == IDOf("2")).Metadata_Tag_EntryID;

        store.SetTags(_series, [On("2"), On("4")]);

        var rows = entries.GetByEntry(_series);
        Assert.Equal([IDOf("2"), IDOf("4")], rows.Select(row => row.TagID));
        Assert.Equal(kept, rows[0].Metadata_Tag_EntryID);
        Assert.Equal(2, entries.GetAll().Count);
        Assert.Equal(2, store.RemoveTags(_series));
        Assert.Empty(entries.GetAll());
    }

    [Fact]
    public void AnIDNamingAnotherKindIsRefused()
    {
        var (store, tags, _, _) = TagStore();
        var before = tags.GetAll().Count;

        Assert.Throws<ArgumentException>(() => store.SaveTags([new() { ID = StudioID("5"), Name = "Not a tag" }]));
        Assert.Equal(before, tags.GetAll().Count);
    }

    [Fact]
    public void AMissingTagChangesNothing()
    {
        var (store, tags, entries, writer) = TagStore();
        store.SetTags(_series, [On("1")]);
        var before = writer.Writes;

        Assert.Throws<ArgumentException>(() => store.SetTags(_series, [On("2"), On("99")]));

        Assert.Equal(before, writer.Writes);
        Assert.Equal([tags.GetByProviderID(TestSources.AniList, "1")!.Metadata_TagID], entries.GetByEntry(_series).Select(row => row.TagID));
    }

    [Fact]
    public void EverySourcesTagsListByNameAndKind()
    {
        var (store, _, _, _) = TagStore();

        Assert.Equal(["Action", "Drama", "Mecha", "Twist"], store.GetAllTags(TestSources.AniList).Select(tag => tag.Name));
        Assert.Equal(["Action", "Drama"], store.GetAllTags(TestSources.AniList, TagKind.Genre).Select(tag => tag.Name));
        Assert.Empty(store.GetAllTags(MetadataSource.TMDB));
    }

    [Fact]
    public void TheEntriesATagIsOnAreFoundFromTheTag()
    {
        var (store, _, _, _) = TagStore();
        store.SetTags(_series, [On("1"), On("2")]);
        store.SetTags(_movie, [On("2")]);

        Assert.Equal([_series, _movie], store.GetEntriesWithTag(TagID("2")).OrderBy(entry => entry.ID));
        Assert.Equal([_series], store.GetEntriesWithTag(TagID("1")));
        Assert.Empty(store.GetEntriesWithTag(TagID("99")));
        Assert.Empty(store.GetEntriesWithTag(new(TestSources.AniList, MetadataEntityType.Studio, "1")));
    }
}
