using System;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers <see cref="MetadataRelationStore"/> and
/// <see cref="MetadataSuggestionStore"/> against in-memory tables: relations
/// read from either end, suggestions from the entry suggested, and both
/// narrowed to the kinds of entry asked for.
/// </summary>
public class MetadataRelationStoreTests
{
    private static readonly MetadataGuid _first = new(TestSources.AniList, MetadataEntityType.Series, "1");

    private static readonly MetadataGuid _second = new(TestSources.AniList, MetadataEntityType.Series, "2");

    private static readonly MetadataGuid _third = new(TestSources.AniList, MetadataEntityType.Series, "3");

    private static readonly MetadataGuid _film = new(TestSources.AniList, MetadataEntityType.Movie, "9");

    private static MetadataRelationStore Relations(out Metadata_RelationRepository repository)
    {
        repository = CachedRepo.Build<Metadata_RelationRepository, int, Metadata_Relation>(row => row.Metadata_RelationID);
        return new(repository, new CacheOnlyRowWriter());
    }

    private static MetadataSuggestionStore Suggestions(out Metadata_SuggestionRepository repository)
    {
        repository = CachedRepo.Build<Metadata_SuggestionRepository, int, Metadata_Suggestion>(row => row.Metadata_SuggestionID);
        return new(repository, new CacheOnlyRowWriter());
    }

    private static MetadataRelationData To(MetadataGuid entry, RelationType relationType)
        => new() { RelatedID = entry, RelationType = relationType };

    private static MetadataSuggestionData Suggest(MetadataGuid entry, int? order = null, SuggestionKind kind = SuggestionKind.Recommended)
        => new() { SuggestedID = entry, Order = order, Kind = kind };

    [Fact]
    public void ARelationStatedFromOneEndIsReadBackReversedFromTheOther()
    {
        var store = Relations(out _);
        store.SetRelations(_first, [To(_second, RelationType.Sequel)]);

        var relation = Assert.Single(store.GetRelations<ISeries, ISeries>(_second));

        Assert.Equal("2", relation.BaseID.ID);
        Assert.Equal("1", relation.RelatedID.ID);
        Assert.Equal(RelationType.Prequel, relation.RelationType);
        Assert.Equal(RelationType.Sequel, relation.Reversed.RelationType);
    }

    [Fact]
    public void AnEntrysOwnRelationsComeFirstAndSpeakForTheirEnd()
    {
        var store = Relations(out _);
        store.SetRelations(_second, [To(_first, RelationType.SideStory), To(_third, RelationType.Sequel)]);
        // The first entry calls the second its sequel, but the second already
        // says what the first is to it, so that is what it reads.
        store.SetRelations(_first, [To(_second, RelationType.Sequel)]);
        store.SetRelations(_third, [To(_film, RelationType.SideStory)]);

        var relations = store.GetRelations<ISeries, ISeries>(_second);

        Assert.Equal(["1", "3"], relations.Select(relation => relation.RelatedID.ID));
        Assert.Equal([RelationType.SideStory, RelationType.Sequel], relations.Select(relation => relation.RelationType));
    }

    [Fact]
    public void RelationsAreNarrowedToTheKindsAskedFor()
    {
        var store = Relations(out _);
        store.SetRelations(_first, [To(_second, RelationType.Sequel), To(_film, RelationType.SideStory)]);

        Assert.Equal(["2"], store.GetRelations<ISeries, ISeries>(_first).Select(relation => relation.RelatedID.ID));
        Assert.Equal(["9"], store.GetRelations<ISeries, IMovie>(_first).Select(relation => relation.RelatedID.ID));
        Assert.Equal(["2", "9"], store.GetRelations<IMetadata, IMetadata>(_first).Select(relation => relation.RelatedID.ID));
        // The first entry is a series, so nothing is read from it as a film.
        Assert.Empty(store.GetRelations<IMovie, ISeries>(_first));
        var reversed = Assert.Single(store.GetRelations<IMovie, ISeries>(_film));
        Assert.Equal(RelationType.MainStory, reversed.RelationType);
    }

    [Fact]
    public void SettingRelationsAgainReplacesThemInTheGivenOrder()
    {
        var store = Relations(out var repository);
        store.SetRelations(_first, [To(_second, RelationType.Sequel), To(_third, RelationType.SameSetting)]);
        var kept = repository.GetByBase(_first).Single(row => row.RelatedID is "3").Metadata_RelationID;

        store.SetRelations(_first, [To(_third, RelationType.SameSetting), To(_film, RelationType.SideStory), To(_film, RelationType.SideStory)]);

        var rows = repository.GetByBase(_first);
        Assert.Equal(["3", "9"], rows.Select(row => row.RelatedID));
        Assert.Equal([0, 1], rows.Select(row => row.Ordering));
        Assert.Equal(kept, rows[0].Metadata_RelationID);
        Assert.Empty(store.GetRelations<ISeries, ISeries>(_second));
        Assert.Equal(2, store.RemoveRelations(_first));
        Assert.Empty(repository.GetAll());
    }

    [Fact]
    public void SuggestionsComeRankedFirstThenInTheGivenOrder()
    {
        var store = Suggestions(out _);
        store.SetSuggestions(_first, [Suggest(_third), Suggest(_second, 1), Suggest(_film), Suggest(_third, 0, SuggestionKind.Similar)]);

        var suggestions = store.GetSuggestions<ISeries, IMetadata>(_first);

        Assert.Equal(["3", "2", "3", "9"], suggestions.Select(suggestion => suggestion.SuggestedID.ID));
        Assert.Equal(
            [SuggestionKind.Similar, SuggestionKind.Recommended, SuggestionKind.Recommended, SuggestionKind.Recommended],
            suggestions.Select(suggestion => suggestion.Kind)
        );
        Assert.Equal([0, 1, null, null], suggestions.Select(suggestion => suggestion.Order));
        Assert.Equal(["9"], store.GetSuggestions<ISeries, IMovie>(_first).Select(suggestion => suggestion.SuggestedID.ID));
        Assert.Empty(store.GetSuggestions<IMovie, IMetadata>(_first));
    }

    [Fact]
    public void AnEntryFindsTheSuggestionsPointingAtIt()
    {
        var store = Suggestions(out _);
        store.SetSuggestions(_first, [Suggest(_third)]);
        store.SetSuggestions(_second, [Suggest(_first), Suggest(_third)]);
        store.SetSuggestions(_film, [Suggest(_third)]);

        var pointing = store.GetSuggestedBy<IMetadata, ISeries>(_third);

        Assert.Equal(["1", "2", "9"], pointing.Select(suggestion => suggestion.BaseID.ID).Order());
        Assert.All(pointing, suggestion => Assert.Equal("3", suggestion.SuggestedID.ID));
        Assert.Equal(["9"], store.GetSuggestedBy<IMovie, ISeries>(_third).Select(suggestion => suggestion.BaseID.ID));
        Assert.Empty(store.GetSuggestedBy<IMetadata, IMovie>(_third));
    }

    [Fact]
    public void SettingSuggestionsAgainReplacesThem()
    {
        var store = Suggestions(out var repository);
        store.SetSuggestions(_first, [Suggest(_second, 0), Suggest(_third, 1)]);

        store.SetSuggestions(_first, [Suggest(_third, 0)]);

        var suggestion = Assert.Single(store.GetSuggestions<ISeries, ISeries>(_first));
        Assert.Equal("3", suggestion.SuggestedID.ID);
        Assert.Equal(0, suggestion.Order);
        Assert.Empty(store.GetSuggestedBy<ISeries, ISeries>(_second));
        Assert.Equal(1, store.RemoveSuggestions(_first));
        Assert.Empty(repository.GetAll());
    }
}
