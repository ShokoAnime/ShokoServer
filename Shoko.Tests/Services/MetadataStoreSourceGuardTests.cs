using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers the guards every typed metadata store shares: a write under a
/// source the core registers, or one naming an entry on another source, is
/// refused before anything is written, while the same write under a plugin's
/// source goes through.
/// </summary>
public class MetadataStoreSourceGuardTests
{
    #region Helpers

    /// <summary>
    /// The sources the core registers and keeps in its own tables.
    /// </summary>
    public static TheoryData<string> CoreSources() => new(MetadataSource.All.Where(source => source.IsCore).Select(source => source.Value));

    private sealed class Stores
    {
        public CacheOnlyRowWriter Writer { get; } = new();

        public MetadataPeopleStore People { get; }

        public MetadataTagStore Tags { get; }

        public MetadataStudioStore Studios { get; }

        public MetadataRelationStore Relations { get; }

        public MetadataSuggestionStore Suggestions { get; }

        public MetadataSeriesStore Series { get; }

        public MetadataMovieStore Movies { get; }

        public MetadataCollectionStore Collections { get; }

        public Stores()
        {
            var texts = new MetadataTextStore(new TextCache(), Writer);
            People = new(
                CachedRepo.Build<Metadata_CreatorRepository, int, Metadata_Creator>(row => row.Metadata_CreatorID),
                CachedRepo.Build<Metadata_CharacterRepository, int, Metadata_Character>(row => row.Metadata_CharacterID),
                new InMemoryCastRepository(),
                new InMemoryCrewRepository(),
                texts,
                Writer
            );
            Tags = new(
                CachedRepo.Build<Metadata_TagRepository, int, Metadata_Tag>(row => row.Metadata_TagID),
                CachedRepo.Build<Metadata_Tag_EntryRepository, int, Metadata_Tag_Entry>(row => row.Metadata_Tag_EntryID),
                Writer,
                texts
            );
            Studios = new(
                CachedRepo.Build<Metadata_StudioRepository, int, Metadata_Studio>(row => row.Metadata_StudioID),
                CachedRepo.Build<Metadata_Studio_EntryRepository, int, Metadata_Studio_Entry>(row => row.Metadata_Studio_EntryID),
                CachedRepo.Build<Metadata_NetworkRepository, int, Metadata_Network>(row => row.Metadata_NetworkID),
                CachedRepo.Build<Metadata_Network_EntryRepository, int, Metadata_Network_Entry>(row => row.Metadata_Network_EntryID),
                Writer,
                texts
            );
            Relations = new(CachedRepo.Build<Metadata_RelationRepository, int, Metadata_Relation>(row => row.Metadata_RelationID), Writer);
            Suggestions = new(CachedRepo.Build<Metadata_SuggestionRepository, int, Metadata_Suggestion>(row => row.Metadata_SuggestionID), Writer);
            Series = new(
                CachedRepo.Build<Metadata_SeriesRepository, int, Metadata_Series>(row => row.Metadata_SeriesID),
                CachedRepo.Build<Metadata_SeasonRepository, int, Metadata_Season>(row => row.Metadata_SeasonID),
                CachedRepo.Build<Metadata_EpisodeRepository, int, Metadata_Episode>(row => row.Metadata_EpisodeID),
                CachedRepo.Build<Metadata_ContentRatingRepository, int, Metadata_ContentRating>(row => row.Metadata_ContentRatingID),
                texts,
                NoCleanup.Build(),
                new Lazy<MetadataOrderingService>(() => new OrderingTables().Build(() => Mock.Of<IMetadataService>())),
                new Lazy<IMetadataCrossReferenceStore>(() => NoLinks.Build().Object),
                new Mock<IQueueScheduler>().Object,
                NullLogger<MetadataSeriesStore>.Instance
            );
            Movies = new(
                CachedRepo.Build<Metadata_MovieRepository, int, Metadata_Movie>(row => row.Metadata_MovieID),
                CachedRepo.Build<Metadata_ContentRatingRepository, int, Metadata_ContentRating>(row => row.Metadata_ContentRatingID),
                texts,
                NoCleanup.Build()
            );
            Collections = new(
                CachedRepo.Build<Metadata_CollectionRepository, int, Metadata_Collection>(row => row.Metadata_CollectionID),
                CachedRepo.Build<Metadata_Collection_MemberRepository, int, Metadata_Collection_Member>(row => row.Metadata_Collection_MemberID),
                texts,
                NoCleanup.Build()
            );
        }

        /// <summary>
        /// Every write the stores take, each for an entry or entity on one source.
        /// </summary>
        public IReadOnlyList<(string Name, Action Write)> Writes(MetadataSource source)
        {
            var series = new MetadataGuid(source, MetadataEntityType.Series, "1");
            return
            [
                ("SaveCreators", () => People.SaveCreators([new() { ID = new(source, MetadataEntityType.Creator, "1"), Name = "Kana" }])),
                ("SaveCharacters", () => People.SaveCharacters([new() { ID = new(source, MetadataEntityType.Character, "1"), Name = "Alice" }])),
                ("SetCast", () => People.SetCast(series, [])),
                ("SetCrew", () => People.SetCrew(series, [])),
                ("RemoveCast", () => People.RemoveCast(series)),
                ("RemoveCrew", () => People.RemoveCrew(series)),
                ("RemoveOrphaned", () => People.RemoveOrphaned(source, DateTime.Now)),
                ("SaveTags", () => Tags.SaveTags([new() { ID = new(source, MetadataEntityType.Tag, "1"), Name = "Mecha" }])),
                ("SetTags", () => Tags.SetTags(series, [])),
                ("RemoveTags", () => Tags.RemoveTags(series)),
                ("SaveStudios", () => Studios.SaveStudios([new() { ID = new(source, MetadataEntityType.Studio, "1"), Name = "Sunrise" }])),
                ("SetStudios", () => Studios.SetStudios(series, [])),
                ("RemoveStudios", () => Studios.RemoveStudios(series)),
                ("SaveNetworks", () => Studios.SaveNetworks([new() { ID = new(source, MetadataEntityType.Network, "1"), Name = "Tokyo MX" }])),
                ("SetNetworks", () => Studios.SetNetworks(series, Array.Empty<MetadataGuid>())),
                ("RemoveNetworks", () => Studios.RemoveNetworks(series)),
                ("RemoveOrphanedStudios", () => Studios.RemoveOrphaned(source, DateTime.Now)),
                ("SetRelations", () => Relations.SetRelations(series, [])),
                ("RemoveRelations", () => Relations.RemoveRelations(series)),
                ("SetSuggestions", () => Suggestions.SetSuggestions(series, [])),
                ("RemoveSuggestions", () => Suggestions.RemoveSuggestions(series)),
                ("SaveSeries", () => Series.SaveSeries(new() { ID = series })),
                ("RemoveSeries", () => Series.RemoveSeries(series)),
                ("SaveMovie", () => Movies.SaveMovie(new() { ID = new(source, MetadataEntityType.Movie, "1") })),
                ("RemoveMovie", () => Movies.RemoveMovie(new(source, MetadataEntityType.Movie, "1"))),
                ("SaveCollection", () => Collections.SaveCollection(new() { ID = new(source, MetadataEntityType.Collection, "1"), Members = [series] })),
                ("RemoveCollection", () => Collections.RemoveCollection(new(source, MetadataEntityType.Collection, "1"))),
            ];
        }

        /// <summary>
        /// Every write for an entry on a plugin's source that names an entry
        /// on another source.
        /// </summary>
        public IReadOnlyList<(string Name, Action Write)> ForeignReferences()
        {
            var series = new MetadataGuid(TestSources.Plugin, MetadataEntityType.Series, "1");
            static MetadataGuid Elsewhere(MetadataEntityType entityType)
                => new(TestSources.AniList, entityType, "1");

            return
            [
                ("SetCast", () => People.SetCast(series, [new() { CharacterID = Elsewhere(MetadataEntityType.Character), Name = "Alice" }])),
                ("SetCrew", () => People.SetCrew(series, [new() { CreatorID = Elsewhere(MetadataEntityType.Creator), Name = "Music" }])),
                ("SetTags", () => Tags.SetTags(series, [new() { TagID = Elsewhere(MetadataEntityType.Tag) }])),
                ("SetStudios", () => Studios.SetStudios(series, [new() { StudioID = Elsewhere(MetadataEntityType.Studio) }])),
                ("SetNetworks", () => Studios.SetNetworks(series, [Elsewhere(MetadataEntityType.Network)])),
                ("SetRelations", () => Relations.SetRelations(series, [new() { RelatedID = Elsewhere(MetadataEntityType.Series), RelationType = RelationType.Other }])),
                ("SetSuggestions", () => Suggestions.SetSuggestions(series, [new() { SuggestedID = Elsewhere(MetadataEntityType.Series) }])),
                ("SaveSeries", () => Series.SaveSeries(new() { ID = series, Seasons = [new() { ID = Elsewhere(MetadataEntityType.Season), SeasonNumber = 1 }] })),
                ("SaveCollection", () => Collections.SaveCollection(new() { ID = new(TestSources.Plugin, MetadataEntityType.Collection, "1"), Members = [Elsewhere(MetadataEntityType.Series)] })),
            ];
        }
    }

    #endregion

    [Theory]
    [MemberData(nameof(CoreSources))]
    public void EveryStoreRefusesToWriteUnderACoreSource(string value)
    {
        var stores = new Stores();
        var source = MetadataSource.Get(value);

        foreach (var (name, write) in stores.Writes(source))
            Assert.True(Record.Exception(write) is ArgumentException, $"{name} was not refused.");

        Assert.Equal(0, stores.Writer.Writes);
    }

    [Fact]
    public void EveryStoreWritesUnderAPluginSource()
    {
        var stores = new Stores();

        foreach (var (_, write) in stores.Writes(TestSources.Plugin))
            write();

        Assert.NotEqual(0, stores.Writer.Writes);
    }

    [Fact]
    public void EveryStoreRefusesAnEntryOnAnotherSource()
    {
        var stores = new Stores();

        foreach (var (name, write) in stores.ForeignReferences())
            Assert.True(Record.Exception(write) is ArgumentException, $"{name} was not refused.");

        Assert.Equal(0, stores.Writer.Writes);
    }
}
