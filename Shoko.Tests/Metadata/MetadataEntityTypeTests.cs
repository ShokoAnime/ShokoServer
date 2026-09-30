using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Metadata;

/// <summary>
/// Covers <see cref="MetadataEntityType"/>: the registry that plugins add to,
/// how old enum spellings read, parsing text into kinds, equality and
/// ordering.
/// </summary>
/// <remarks>
/// The registry is process-global and never forgets a kind, so every test
/// works with values of its own, prefixed with <c>met-</c> and the test's
/// name, and can run in parallel with the rest.
/// </remarks>
public class MetadataEntityTypeTests
{
    #region Lookups

    [Theory]
    [InlineData(" series ", "series")]
    [InlineData("Collection", "collection")]
    [InlineData("Group", "collection")]
    [InlineData("BoxSet", "collection")]
    [InlineData("Franchise", "collection")]
    [InlineData("Series", "series")]
    [InlineData("Anime", "series")]
    [InlineData("Show", "series")]
    [InlineData("Season", "season")]
    [InlineData("Episode", "episode")]
    [InlineData("Movie", "movie")]
    [InlineData("Video", "video")]
    [InlineData("Company", "studio")]
    [InlineData("Studio", "studio")]
    [InlineData("Network", "network")]
    [InlineData("Channel", "channel")]
    [InlineData("Creator", "creator")]
    [InlineData("Person", "creator")]
    [InlineData("StaffMember", "creator")]
    [InlineData("Character", "character")]
    [InlineData("User", "user")]
    public void EveryOldEnumSpellingButUnknownAndLibraryReadsAsACoreKind(string text, string value)
    {
        Assert.Same(MetadataEntityType.Get(value), MetadataEntityType.Get(text));
        Assert.Same(MetadataEntityType.Get(value), MetadataEntityType.Parse(text));
    }

    [Fact]
    public void TheOldUnknownSpellingParsesAsAnUnregisteredKind()
    {
        var parsed = MetadataEntityType.Parse("Unknown");

        Assert.Equal("unknown", parsed.Value);
        Assert.False(parsed.IsRegistered);
    }

    [Fact]
    public void GetDoesNotFindAKindByItsName()
    {
        MetadataEntityType.Register("Met Named Kind", "met-named-kind");

        Assert.False(MetadataEntityType.TryGet("Met Named Kind", out _));
        Assert.Throws<KeyNotFoundException>(() => MetadataEntityType.Get("Met Named Kind"));
    }

    [Fact]
    public void GetThrowsForTextThatIsNotRegistered()
        => Assert.Throws<KeyNotFoundException>(() => MetadataEntityType.Get("met-get-throws-unknown"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("met-try-get-unknown")]
    public void TryGetFailsForEmptyOrUnknownText(string? text)
    {
        Assert.False(MetadataEntityType.TryGet(text, out var entityType));
        Assert.Null(entityType);
    }

    [Fact]
    public void TryGetDoesNotMistakeAnUnregisteredKindForARegisteredOne()
    {
        MetadataEntityType.Parse("met-try-get-unregistered");

        Assert.False(MetadataEntityType.TryGet("met-try-get-unregistered", out _));
    }

    #endregion

    #region Register

    [Fact]
    public void RegisterAddsANewKind()
    {
        var entityType = MetadataEntityType.Register("MetRegisterNew", "met-register-new", ["met-register-new-alias"]);

        Assert.Equal("met-register-new", entityType.Value);
        Assert.Equal("MetRegisterNew", entityType.Name);
        Assert.Equal(["met-register-new-alias"], entityType.Aliases);
        Assert.True(entityType.IsRegistered);
        Assert.Contains(entityType, MetadataEntityType.All);
        Assert.Same(entityType, MetadataEntityType.Get("MET-REGISTER-NEW-ALIAS"));
    }

    [Fact]
    public void RegisterLeavesTheValueOutOfTheAliasesAndTrims()
    {
        var entityType = MetadataEntityType.Register("  Met Dedupe  ", "met-dedupe", ["  metdedupe ", "MET-DEDUPE", "metdedupe"]);

        Assert.Equal("Met Dedupe", entityType.Name);
        Assert.Equal(["metdedupe"], entityType.Aliases);
    }

    [Fact]
    public void RegisterAcceptsAValueOfTheMaximumLength()
    {
        var value = "met-max-" + new string('a', MetadataEntityType.MaxValueLength - 8);

        Assert.Equal(value, MetadataEntityType.Register("MetMax", value).Value);
    }

    [Fact]
    public void ConcurrentRegistrationsOfOneKindShareOneInstance()
    {
        var entityTypes = new MetadataEntityType[32];
        Parallel.For(0, entityTypes.Length, i => entityTypes[i] = MetadataEntityType.Register("MetConcurrent", "met-concurrent"));

        Assert.All(entityTypes, entityType => Assert.Same(entityTypes[0], entityType));
        Assert.Single(MetadataEntityType.All, entityType => entityType.Value == "met-concurrent");
    }

    [Fact]
    public void RegisterRaisesRegisteredAndSaysWhetherItMerged()
    {
        var events = new List<(string Value, bool Merged)>();
        void OnRegistered(MetadataEntityType entityType, bool merged)
        {
            if (entityType.Value != "met-event")
                return;

            lock (events)
                events.Add((entityType.Value, merged));
        }

        MetadataEntityType.Registered += OnRegistered;
        try
        {
            MetadataEntityType.Register("MetEvent", "met-event");
            MetadataEntityType.Register("MetEvent", "met-event");
            MetadataEntityType.Register("MetEvent", "met-event", ["met-event-alias"]);
        }
        finally
        {
            MetadataEntityType.Registered -= OnRegistered;
        }

        // The repeat changed nothing, so it raised nothing.
        Assert.Equal([("met-event", false), ("met-event", true)], events);
    }

    [Fact]
    public void ASecondRegistrationAddsItsNewAliasesAndKeepsTheFirstName()
    {
        var first = MetadataEntityType.Register("MetMerge", "met-merge", ["met-merge-a"]);
        var second = MetadataEntityType.Register("Met Merge Other", "met-merge", ["met-merge-a", "met-merge-b"]);

        Assert.Same(first, second);
        Assert.Equal("MetMerge", first.Name);
        Assert.Equal(["met-merge-a", "met-merge-b"], first.Aliases);
        Assert.Same(first, MetadataEntityType.Get("met-merge-b"));
    }

    [Fact]
    public void APluginCanRegisterALibraryKindThatSortsWhereTheOldMemberDid()
    {
        var library = TestEntityTypes.Library;

        Assert.Same(library, MetadataEntityType.Get("Library"));
        Assert.Same(library, MetadataEntityType.Parse("library"));
        Assert.True(library.CompareTo(MetadataEntityType.User) > 0);
        Assert.True(library.CompareTo(MetadataEntityType.Tag) < 0);
    }

    #endregion

    #region Underscores

    [Theory]
    [InlineData("met_underscore_value")]
    [InlineData("MET_UNDERSCORE_VALUE")]
    [InlineData("met_underscore_alias")]
    [InlineData("MET-UNDERSCORE_ALIAS")]
    public void GetReadsAnUnderscoreAsAHyphenInAValueOrAnAlias(string text)
    {
        var entityType = MetadataEntityType.Register("MetUnderscore", "met-underscore-value", ["met-underscore-alias"]);

        Assert.Same(entityType, MetadataEntityType.Get(text));
        Assert.True(MetadataEntityType.TryGet(text, out var found));
        Assert.Same(entityType, found);
        Assert.Same(entityType, MetadataEntityType.Parse(text));
    }

    [Theory]
    [InlineData("STAFF_MEMBER", false)]
    [InlineData("staffmember", true)]
    public void GetReadsUnderscoresInCoreKeysAsHyphensOnly(string text, bool found)
        => Assert.Equal(found, MetadataEntityType.TryGet(text, out _));

    [Fact]
    public void AnAliasRegisteredWithUnderscoresIsFoundWithHyphens()
    {
        var entityType = MetadataEntityType.Register("MetUnderscoredAlias", "met-underscored-alias-owner", ["met_underscored_alias"]);

        Assert.Equal(["met_underscored_alias"], entityType.Aliases);
        Assert.Same(entityType, MetadataEntityType.Get("met-underscored-alias"));
        Assert.Same(entityType, MetadataEntityType.Get("MET_UNDERSCORED_ALIAS"));
    }

    [Fact]
    public void AliasesThatDifferOnlyByUnderscoresAreOneAlias()
    {
        var entityType = MetadataEntityType.Register("MetUnderscoreDedupe", "met-underscore-dedupe", ["met_underscore_dedupe_alias", "MET-UNDERSCORE-DEDUPE-ALIAS", "met_underscore_dedupe"]);

        Assert.Equal(["met_underscore_dedupe_alias"], entityType.Aliases);
    }

    [Theory]
    [InlineData("MET_PARSED_UNDERSCORES")]
    [InlineData("met_parsed-underscores")]
    public void ParsingUnregisteredTextWithUnderscoresGivesTheHyphenatedValue(string text)
    {
        var entityType = MetadataEntityType.Parse(text);

        Assert.Equal("met-parsed-underscores", entityType.Value);
        Assert.False(entityType.IsRegistered);
        Assert.Same(entityType, MetadataEntityType.Parse("met-parsed-underscores"));
    }

    #endregion

    #region Descriptions

    [Fact]
    public void RegisterKeepsTheDescriptionTrimmedOrNone()
    {
        Assert.Equal("A kind for the tests.", MetadataEntityType.Register("MetDescribed", "met-described", description: " A kind for the tests. ").Description);
        Assert.Null(MetadataEntityType.Register("MetUndescribed", "met-undescribed").Description);
        Assert.Null(MetadataEntityType.Register("MetBlankDescribed", "met-blank-described", description: "  ").Description);
    }

    [Fact]
    public void ASecondRegistrationFillsAnEmptyDescriptionAndKeepsAnyOther()
    {
        var merges = new List<bool>();
        void OnRegistered(MetadataEntityType entityType, bool merged)
        {
            if (entityType.Value == "met-description-merge")
                lock (merges)
                    merges.Add(merged);
        }

        MetadataEntityType.Registered += OnRegistered;
        try
        {
            var first = MetadataEntityType.Register("MetDescriptionMerge", "met-description-merge");
            Assert.Null(first.Description);

            Assert.Same(first, MetadataEntityType.Register("MetDescriptionMerge", "met-description-merge", description: "Filled in later."));
            Assert.Equal("Filled in later.", first.Description);

            MetadataEntityType.Register("MetDescriptionMerge", "met-description-merge", description: "Too late.");
            Assert.Equal("Filled in later.", first.Description);
        }
        finally
        {
            MetadataEntityType.Registered -= OnRegistered;
        }

        Assert.Equal([false, true], merges);
    }

    [Fact]
    public void ACoreKindKeepsItsDescription()
    {
        var description = MetadataEntityType.Series.Description;

        Assert.Same(MetadataEntityType.Series, MetadataEntityType.Register("Series", "series", description: "Something else."));
        Assert.Equal(description, MetadataEntityType.Series.Description);
    }

    [Fact]
    public void AParsedKindTakesTheDescriptionOfItsLaterRegistration()
    {
        var parsed = MetadataEntityType.Parse("met-described-late");
        Assert.Null(parsed.Description);

        MetadataEntityType.Register("MetDescribedLate", "met-described-late", description: "Registered after parsing.");

        Assert.Equal("Registered after parsing.", parsed.Description);
    }

    #endregion

    #region Refusals

    [Theory]
    [InlineData("MetBadValue")]
    [InlineData("met bad value")]
    [InlineData("met_bad_value")]
    [InlineData("-met-bad-value")]
    [InlineData("met-bad-value-")]
    [InlineData("met--bad-value")]
    [InlineData("")]
    [InlineData("   ")]
    public void RegisterRefusesAnInvalidValue(string value)
        => Assert.ThrowsAny<ArgumentException>(() => MetadataEntityType.Register("MetBadValue", value));

    [Fact]
    public void RegisterRefusesAValueThatIsTooLong()
        => Assert.Throws<ArgumentException>(() => MetadataEntityType.Register("MetTooLong", "met-too-long-" + new string('a', MetadataEntityType.MaxValueLength)));

    [Theory]
    [InlineData("")]
    [InlineData("Met\tBadName")]
    [InlineData("Met\u0001BadName")]
    public void RegisterRefusesAnInvalidName(string name)
    {
        Assert.ThrowsAny<ArgumentException>(() => MetadataEntityType.Register(name, "met-bad-name"));
        Assert.False(MetadataEntityType.TryGet("met-bad-name", out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("met bad alias")]
    [InlineData(null)]
    public void RegisterRefusesAnInvalidAlias(string? alias)
    {
        Assert.Throws<ArgumentException>(() => MetadataEntityType.Register("MetBadAlias", "met-bad-alias", [alias!]));
        Assert.False(MetadataEntityType.TryGet("met-bad-alias", out _));
    }

    [Theory]
    // Another kind's alias, in any case or with underscores.
    [InlineData("met-taken-thief", "MET-TAKEN-OWNED")]
    [InlineData("met-taken-thief", "MET_TAKEN_OWNED")]
    // A core kind's value or alias.
    [InlineData("met-taken-thief", "show")]
    [InlineData("met-taken-thief", "Network")]
    [InlineData("person", null)]
    // How a stored number without a kind reads back.
    [InlineData("unknown-5", null)]
    [InlineData("met-taken-thief", "Unknown_8")]
    public void RegisterRefusesAValueOrAliasTakenOrReserved(string value, string? alias)
    {
        var owner = MetadataEntityType.Register("MetTakenOwner", "met-taken-owner", ["met-taken-owned"]);

        Assert.Throws<ArgumentException>(() => MetadataEntityType.Register("MetTakenThief", value, alias is null ? [] : [alias]));
        Assert.False(MetadataEntityType.TryGet("met-taken-thief", out _));
        Assert.Same(owner, MetadataEntityType.Get("met-taken-owned"));
        Assert.Same(MetadataEntityType.Series, MetadataEntityType.Get("show"));
        Assert.Same(MetadataEntityType.Creator, MetadataEntityType.Get("person"));
    }

    [Fact]
    public void AMergeRefusesAnAliasOwnedByAnotherKindAndKeepsNothing()
    {
        MetadataEntityType.Register("MetMergeOwner", "met-merge-owner", ["met-merge-owned"]);
        var entityType = MetadataEntityType.Register("MetMergeThief", "met-merge-thief");

        Assert.Throws<ArgumentException>(() => MetadataEntityType.Register("MetMergeThief", "met-merge-thief", ["met-merge-new", "met-merge-owned"]));
        Assert.Empty(entityType.Aliases);
        Assert.False(MetadataEntityType.TryGet("met-merge-new", out _));
    }

    [Fact]
    public void RegisterRefusesAnAliasThatParseAlreadyHandedOutAsAKindOfItsOwn()
    {
        var handedOut = MetadataEntityType.Parse("met-handed-out");

        Assert.Throws<ArgumentException>(() => MetadataEntityType.Register("MetHandedOutTaker", "met-handed-out-taker", ["met-handed-out"]));
        Assert.Same(handedOut, MetadataEntityType.Parse("met-handed-out"));
    }

    [Theory]
    [InlineData("series", "met-core-alias")]
    [InlineData("collection", "met-core-alias")]
    [InlineData("channel", "met-core-alias")]
    public void RegisterRefusesToAddAnAliasToACoreKind(string value, string alias)
    {
        var core = MetadataEntityType.Get(value);
        var before = core.Aliases;

        Assert.Throws<ArgumentException>(() => MetadataEntityType.Register(core.Name, value, [alias]));
        Assert.Equal(before, core.Aliases);
        Assert.False(MetadataEntityType.TryGet(alias, out _));
    }

    [Fact]
    public void RepeatingACoreRegistrationReturnsTheCoreKind()
    {
        Assert.Same(MetadataEntityType.Series, MetadataEntityType.Register("Series", "series"));
        Assert.Same(MetadataEntityType.Series, MetadataEntityType.Register("Anime", "series", ["Show"]));
        Assert.Equal("Series", MetadataEntityType.Series.Name);
    }

    #endregion

    #region Exact Values

    [Fact]
    public void GetByValueGivesTheRegisteredInstanceAndSkipsAliases()
    {
        Assert.Same(MetadataEntityType.Series, MetadataEntityType.GetByValue("series"));

        var exact = MetadataEntityType.GetByValue("show");

        Assert.Equal("show", exact.Value);
        Assert.False(exact.IsRegistered);
        Assert.NotEqual(MetadataEntityType.Series, exact);
    }

    [Theory]
    [InlineData("Series")]
    [InlineData("series ")]
    [InlineData("")]
    public void GetByValueRefusesAnythingButAValue(string text)
        => Assert.Throws<ArgumentException>(() => MetadataEntityType.GetByValue(text));

    #endregion

    #region Parse

    [Fact]
    public void ParseGivesAnUnregisteredKindForAnyOtherValidValue()
    {
        var entityType = MetadataEntityType.Parse(" MET-Parse-Some-Thing ");

        Assert.Equal("met-parse-some-thing", entityType.Value);
        Assert.Equal("MetParseSomeThing", entityType.Name);
        Assert.Empty(entityType.Aliases);
        Assert.False(entityType.IsRegistered);
        Assert.DoesNotContain(entityType, MetadataEntityType.All);
        Assert.Same(entityType, MetadataEntityType.Parse("met-parse-some-thing"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("met parse bad")]
    [InlineData("met_parse_bad_")]
    [InlineData("met__parse_bad")]
    [InlineData("met-parse-bad-")]
    [InlineData("met-parse-bad-ä")]
    public void ParseRefusesInvalidText(string text)
    {
        Assert.Throws<FormatException>(() => MetadataEntityType.Parse(text));
        Assert.False(MetadataEntityType.TryParse(text, out var entityType));
        Assert.Null(entityType);
    }

    [Fact]
    public void AnUnregisteredKindReadsThroughALaterRegistration()
    {
        var early = MetadataEntityType.Parse("met-late");
        Assert.Equal("MetLate", early.Name);

        var registered = MetadataEntityType.Register("Met Late Registered", "met-late", ["met-late-alias"]);

        Assert.NotSame(early, registered);
        Assert.Equal(registered, early);
        Assert.Equal("Met Late Registered", early.Name);
        Assert.Equal(["met-late-alias"], early.Aliases);
        Assert.True(early.IsRegistered);
        Assert.Same(registered, MetadataEntityType.Parse("met-late"));
    }

    [Fact]
    public async Task ParseAndRegisterNeverBothClaimTheSameText()
    {
        var token = TestContext.Current.CancellationToken;
        for (var i = 0; i < 20; i++)
        {
            var alias = $"met-race-alias-{i}";
            MetadataEntityType? parsed = null;
            MetadataEntityType? registered = null;
            using var barrier = new Barrier(2);
            var parse = Task.Run(() =>
            {
                barrier.SignalAndWait(token);
                parsed = MetadataEntityType.Parse(alias);
            }, token);
            var register = Task.Run(() =>
            {
                barrier.SignalAndWait(token);
                try
                {
                    registered = MetadataEntityType.Register($"MetRaceOwner{i}", $"met-race-owner-{i}", [alias]);
                }
                catch (ArgumentException)
                {
                }
            }, token);
            await Task.WhenAll(parse, register);

            if (registered is not null)
                Assert.Equal(registered, parsed);
            else
                Assert.Equal(alias, parsed!.Value);
        }
    }

    #endregion

    #region Equality

    [Fact]
    public void KindsWithTheSameValueAreEqual()
    {
        var early = MetadataEntityType.Parse("met-equal-early");
        var registered = MetadataEntityType.Register("MetEqualEarly", "met-equal-early");

        Assert.True(early.Equals(registered));
        Assert.True(early.Equals((object)registered));
        Assert.True(early == registered);
        Assert.False(early != registered);
        Assert.Equal(early.GetHashCode(), registered.GetHashCode());
    }

    [Fact]
    public void ADifferentValueOrNullIsNotEqual()
    {
        MetadataEntityType? none = null;

        Assert.True(MetadataEntityType.Network != MetadataEntityType.Channel);
        Assert.False(MetadataEntityType.Series.Equals((object)"series"));
        Assert.False(MetadataEntityType.Series == none);
        Assert.True(none == null);
    }

    [Fact]
    public void KindsWorkAsDictionaryKeysAcrossInstances()
    {
        var early = MetadataEntityType.Parse("met-dictionary-key");
        var dictionary = new Dictionary<MetadataEntityType, int> { [early] = 1 };
        var registered = MetadataEntityType.Register("MetDictionaryKey", "met-dictionary-key");

        Assert.Equal(1, dictionary[registered]);
    }

    [Fact]
    public void SortingFollowsTheOldEnumOrderThenTheNewKindsByValue()
    {
        var newer = MetadataEntityType.Parse("met-sort-newer");
        var sorted = new[]
        {
            MetadataEntityType.Tag,
            newer,
            MetadataEntityType.Filter,
            TestEntityTypes.Library,
            MetadataEntityType.User,
            MetadataEntityType.Series,
            MetadataEntityType.Collection,
        }.Order().ToList();

        // The old members first, a plugin's library where the old member was, the new kinds by value, and tags last.
        Assert.True(MetadataEntityType.Series.CompareTo(null) > 0);
        Assert.Equal(
            [
                MetadataEntityType.Collection,
                MetadataEntityType.Series,
                MetadataEntityType.User,
                TestEntityTypes.Library,
                MetadataEntityType.Filter,
                newer,
                MetadataEntityType.Tag,
            ],
            sorted
        );
    }

    #endregion
}
