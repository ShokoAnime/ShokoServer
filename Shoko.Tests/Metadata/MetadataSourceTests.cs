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
/// Covers <see cref="MetadataSource"/>: the registry that plugins add to,
/// parsing text into sources, equality and ordering.
/// </summary>
/// <remarks>
/// The registry is process-global and never forgets a source, so every test
/// works with values of its own, prefixed with <c>mst-</c> and the test's
/// name, and can run in parallel with the rest.
/// </remarks>
public class MetadataSourceTests
{
    #region Lookups

    [Theory]
    [InlineData("ANIDB", "anidb")]
    [InlineData(" anidb ", "anidb")]
    [InlineData("TheMovieDB", "tmdb")]
    [InlineData("Locally-Generated", "generated")]
    [InlineData("LOCALLY_GENERATED", "generated")]
    public void GetFindsACoreSourceByValueOrAliasIgnoringCaseAndUnderscores(string text, string value)
        => Assert.Same(MetadataSource.GetByValue(value), MetadataSource.Get(text));

    [Theory]
    [InlineData("Locally Generated")]
    [InlineData("TestPlugin")]
    public void GetDoesNotFindASourceByItsName(string text)
    {
        _ = TestSources.Plugin;

        Assert.False(MetadataSource.TryGet(text, out _));
        Assert.Throws<KeyNotFoundException>(() => MetadataSource.Get(text));
    }

    [Fact]
    public void GetThrowsForTextThatIsNotRegistered()
        => Assert.Throws<KeyNotFoundException>(() => MetadataSource.Get("mst-get-throws-unknown"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("mst-try-get-unknown")]
    public void TryGetFailsForEmptyOrUnknownText(string? text)
    {
        Assert.False(MetadataSource.TryGet(text, out var source));
        Assert.Null(source);
    }

    [Fact]
    public void TryGetDoesNotMistakeAnUnregisteredSourceForARegisteredOne()
    {
        MetadataSource.Parse("mst-try-get-unregistered");

        Assert.False(MetadataSource.TryGet("mst-try-get-unregistered", out _));
    }

    #endregion

    #region Register

    [Fact]
    public void RegisterAddsANewSource()
    {
        var source = MetadataSource.Register("MstRegisterNew", "mst-register-new", ["mst-register-new-alias"]);

        Assert.Equal("mst-register-new", source.Value);
        Assert.Equal("MstRegisterNew", source.Name);
        Assert.Equal(["mst-register-new-alias"], source.Aliases);
        Assert.False(source.IsLocal);
        Assert.True(source.IsRemote);
        Assert.True(source.IsRegistered);
        Assert.Contains(source, MetadataSource.All);
    }

    [Fact]
    public void ARegisteredSourceIsFoundByValueAndAliasIgnoringCaseButNotByName()
    {
        var source = MetadataSource.Register("MstFound", "mst-found", ["mst-found-alias"]);

        Assert.Same(source, MetadataSource.Get("mst-found"));
        Assert.Same(source, MetadataSource.Get("MST-FOUND"));
        Assert.Same(source, MetadataSource.Get("Mst-Found-Alias"));
        Assert.True(MetadataSource.TryGet("mst-found-alias", out var found));
        Assert.Same(source, found);
        Assert.False(MetadataSource.TryGet("MstFound", out _));
    }

    [Fact]
    public void RegisterKeepsTheLocalFlag()
    {
        var source = MetadataSource.Register("MstLocal", "mst-local", local: true);

        Assert.True(source.IsLocal);
        Assert.False(source.IsRemote);
    }

    [Fact]
    public void RegisterLeavesTheValueOutOfTheAliases()
    {
        var source = MetadataSource.Register("MstDedupe", "mst-dedupe", ["mstdedupe", "MST-DEDUPE", "mst-dedupe-other", "MST-DEDUPE-OTHER"]);

        // The name is no key of its own, so an alias spelled like it is kept.
        Assert.Equal(["mstdedupe", "mst-dedupe-other"], source.Aliases);
    }

    [Fact]
    public void RegisterAcceptsACoreSourcesNameWithoutShadowingIt()
    {
        var source = MetadataSource.Register("TMDB", "mst-same-name-as-core");

        Assert.Equal("TMDB", source.Name);
        Assert.Same(MetadataSource.TMDB, MetadataSource.Get("TMDB"));
    }

    [Fact]
    public void RegisterTrimsTheNameAndAliases()
    {
        var source = MetadataSource.Register("  Mst Trimmed  ", "mst-trimmed", ["  mst-trimmed-alias  "]);

        Assert.Equal("Mst Trimmed", source.Name);
        Assert.Equal(["mst-trimmed-alias"], source.Aliases);
        Assert.False(MetadataSource.TryGet("Mst Trimmed", out _));
    }

    [Fact]
    public void RegisterAcceptsAValueOfTheMaximumLength()
    {
        var value = "mst-max-" + new string('a', MetadataSource.MaxValueLength - 8);

        Assert.Equal(value, MetadataSource.Register("MstMax", value).Value);
    }

    [Fact]
    public void ConcurrentRegistrationsOfOneSourceShareOneInstance()
    {
        var sources = new MetadataSource[32];
        Parallel.For(0, sources.Length, i => sources[i] = MetadataSource.Register("MstConcurrent", "mst-concurrent"));

        Assert.All(sources, source => Assert.Same(sources[0], source));
        Assert.Single(MetadataSource.All, source => source.Value == "mst-concurrent");
    }

    [Fact]
    public void RegisterRaisesRegisteredAndSaysWhetherItMerged()
    {
        var events = new List<(string Value, bool Merged)>();
        void OnRegistered(MetadataSource source, bool merged)
        {
            if (source.Value != "mst-event")
                return;

            lock (events)
                events.Add((source.Value, merged));
        }

        MetadataSource.Registered += OnRegistered;
        try
        {
            MetadataSource.Register("MstEvent", "mst-event");
            MetadataSource.Register("MstEvent", "mst-event");
            MetadataSource.Register("MstEvent", "mst-event", ["mst-event-alias"]);
        }
        finally
        {
            MetadataSource.Registered -= OnRegistered;
        }

        // The repeat changed nothing, so it raised nothing.
        Assert.Equal([("mst-event", false), ("mst-event", true)], events);
    }

    #endregion

    #region Merging

    [Fact]
    public void ASecondRegistrationAddsItsNewAliases()
    {
        var first = MetadataSource.Register("MstMergeAliases", "mst-merge-aliases", ["mst-merge-aliases-a"]);
        var second = MetadataSource.Register("MstMergeAliases", "mst-merge-aliases", ["mst-merge-aliases-a", "mst-merge-aliases-b"]);

        Assert.Same(first, second);
        Assert.Equal(["mst-merge-aliases-a", "mst-merge-aliases-b"], first.Aliases);
        Assert.Same(first, MetadataSource.Get("mst-merge-aliases-b"));
    }

    [Fact]
    public void ASecondRegistrationWithAnotherNameKeepsTheFirstName()
    {
        var first = MetadataSource.Register("MstMergeName", "mst-merge-name");
        var second = MetadataSource.Register("Mst Merge Other Name", "mst-merge-name");

        Assert.Same(first, second);
        Assert.Equal("MstMergeName", first.Name);
        Assert.Empty(first.Aliases);
    }

    [Fact]
    public void AnIdenticalRegistrationReturnsTheSameInstanceUnchanged()
    {
        var first = MetadataSource.Register("MstIdentical", "mst-identical", ["mst-identical-alias"], local: true);
        var aliases = first.Aliases;
        var second = MetadataSource.Register("MstIdentical", "mst-identical", ["mst-identical-alias"], local: true);

        Assert.Same(first, second);
        Assert.Same(aliases, first.Aliases);
        Assert.Single(MetadataSource.All, source => source.Value == "mst-identical");
    }

    #endregion

    #region Underscores

    [Theory]
    [InlineData("mst_underscore_value")]
    [InlineData("MST_UNDERSCORE_VALUE")]
    [InlineData("Mst_Underscore-Value")]
    [InlineData("mst_underscore_alias")]
    [InlineData("MST-UNDERSCORE_ALIAS")]
    public void GetReadsAnUnderscoreAsAHyphenInAValueOrAnAlias(string text)
    {
        var source = MetadataSource.Register("MstUnderscore", "mst-underscore-value", ["mst-underscore-alias"]);

        Assert.Same(source, MetadataSource.Get(text));
        Assert.True(MetadataSource.TryGet(text, out var found));
        Assert.Same(source, found);
        Assert.Same(source, MetadataSource.Parse(text));
    }

    [Theory]
    [InlineData("mst-underscored-alias")]
    [InlineData("MST_UNDERSCORED_ALIAS")]
    public void AnAliasRegisteredWithUnderscoresIsFoundWithHyphens(string text)
    {
        var source = MetadataSource.Register("MstUnderscoredAlias", "mst-underscored-alias-owner", ["mst_underscored_alias"]);

        Assert.Equal(["mst_underscored_alias"], source.Aliases);
        Assert.Same(source, MetadataSource.Get(text));
    }

    [Fact]
    public void AliasesThatDifferOnlyByUnderscoresAreOneAlias()
    {
        var source = MetadataSource.Register("MstUnderscoreDedupe", "mst-underscore-dedupe", ["mst_underscore_dedupe_alias", "MST-UNDERSCORE-DEDUPE-ALIAS", "mst_underscore_dedupe"]);

        Assert.Equal(["mst_underscore_dedupe_alias"], source.Aliases);
        Assert.Same(source, MetadataSource.Register("MstUnderscoreDedupe", "mst-underscore-dedupe", ["mst-underscore-dedupe-alias"]));
        Assert.Equal(["mst_underscore_dedupe_alias"], source.Aliases);
    }

    [Theory]
    [InlineData("MST_PARSED_UNDERSCORES")]
    [InlineData("mst_parsed_underscores")]
    [InlineData(" mst_parsed-underscores ")]
    public void ParsingUnregisteredTextWithUnderscoresGivesTheHyphenatedValue(string text)
    {
        var source = MetadataSource.Parse(text);

        Assert.Equal("mst-parsed-underscores", source.Value);
        Assert.Equal("MstParsedUnderscores", source.Name);
        Assert.False(source.IsRegistered);
        Assert.Same(source, MetadataSource.Parse("mst-parsed-underscores"));
        Assert.True(MetadataSource.TryParse(text, out var parsed));
        Assert.Same(source, parsed);
    }

    #endregion

    #region Descriptions

    [Fact]
    public void RegisterKeepsTheDescriptionTrimmed()
    {
        var source = MetadataSource.Register("MstDescribed", "mst-described", description: "  A source for the tests.  ");

        Assert.Equal("A source for the tests.", source.Description);
    }

    [Fact]
    public void ASecondRegistrationFillsAnEmptyDescription()
    {
        var merges = new List<bool>();
        void OnRegistered(MetadataSource source, bool merged)
        {
            if (source.Value == "mst-description-filled")
                lock (merges)
                    merges.Add(merged);
        }

        MetadataSource.Registered += OnRegistered;
        try
        {
            var first = MetadataSource.Register("MstDescriptionFilled", "mst-description-filled");
            Assert.Null(first.Description);

            MetadataSource.Register("MstDescriptionFilled", "mst-description-filled", description: "   ");
            Assert.Null(first.Description);

            MetadataSource.Register("MstDescriptionFilled", "mst-description-filled", description: "Filled in later.");
            Assert.Equal("Filled in later.", first.Description);

            MetadataSource.Register("MstDescriptionFilled", "mst-description-filled", description: "Too late.");
            Assert.Equal("Filled in later.", first.Description);
        }
        finally
        {
            MetadataSource.Registered -= OnRegistered;
        }

        Assert.Equal([false, true], merges);
    }

    [Fact]
    public void ACoreSourceKeepsItsDescription()
    {
        var description = MetadataSource.AniDB.Description;

        Assert.Same(MetadataSource.AniDB, MetadataSource.Register("AniDB", "anidb", description: "Something else."));
        Assert.Equal(description, MetadataSource.AniDB.Description);
    }

    [Fact]
    public void AParsedSourceTakesTheDescriptionOfItsLaterRegistration()
    {
        var parsed = MetadataSource.Parse("mst-described-late");
        Assert.Null(parsed.Description);

        MetadataSource.Register("MstDescribedLate", "mst-described-late", description: "Registered after parsing.");

        Assert.Equal("Registered after parsing.", parsed.Description);
    }

    #endregion

    #region Refusals

    [Theory]
    [InlineData("MstBadValue")]
    [InlineData("mst bad value")]
    [InlineData("mst_bad_value")]
    [InlineData("mst.bad.value")]
    [InlineData("-mst-bad-value")]
    [InlineData("mst-bad-value-")]
    [InlineData("mst--bad-value")]
    [InlineData(" mst-bad-value")]
    [InlineData("")]
    [InlineData("   ")]
    public void RegisterRefusesAnInvalidValue(string value)
    {
        Assert.ThrowsAny<ArgumentException>(() => MetadataSource.Register("MstBadValue", value));
        Assert.False(MetadataSource.TryGet("MstBadValue", out _));
    }

    [Fact]
    public void RegisterRefusesAValueThatIsTooLong()
        => Assert.Throws<ArgumentException>(() => MetadataSource.Register("MstTooLong", "mst-too-long-" + new string('a', MetadataSource.MaxValueLength)));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Mst\tBadName")]
    [InlineData("Mst\nBadName")]
    [InlineData("Mst\u00A0BadName")]
    [InlineData("Mst\u0001BadName")]
    public void RegisterRefusesAnInvalidName(string name)
    {
        Assert.ThrowsAny<ArgumentException>(() => MetadataSource.Register(name, "mst-bad-name"));
        Assert.False(MetadataSource.TryGet("mst-bad-name", out _));
    }

    [Fact]
    public void RegisterRefusesANameThatIsTooLong()
        => Assert.Throws<ArgumentException>(() => MetadataSource.Register(new string('N', MetadataSource.MaxValueLength + 1), "mst-long-name"));

    [Theory]
    [InlineData("")]
    [InlineData("mst bad alias")]
    [InlineData(null)]
    public void RegisterRefusesAnInvalidAlias(string? alias)
    {
        Assert.Throws<ArgumentException>(() => MetadataSource.Register("MstBadAlias", "mst-bad-alias", [alias!]));
        Assert.False(MetadataSource.TryGet("mst-bad-alias", out _));
    }

    [Theory]
    // Another source's alias or value, in any case or with underscores.
    [InlineData("mst-taken-thief", "MST-TAKEN-OWNED")]
    [InlineData("mst-taken-thief", "MST_TAKEN_OWNED")]
    [InlineData("mst-taken-thief", "mst_taken_owner")]
    [InlineData("mst-taken-owned", null)]
    // A core source's value or alias.
    [InlineData("mst-taken-thief", "TMDB")]
    [InlineData("mst-taken-thief", "LocallyGenerated")]
    [InlineData("themoviedb", null)]
    // How a stored number without a source reads back.
    [InlineData("unknown-5", null)]
    [InlineData("mst-taken-thief", "Unknown_7")]
    // A word the metadata routes use.
    [InlineData("provider", null)]
    [InlineData("mst-taken-thief", "Provider")]
    public void RegisterRefusesAValueOrAliasTakenOrReserved(string value, string? alias)
    {
        var owner = MetadataSource.Register("MstTakenOwner", "mst-taken-owner", ["mst-taken-owned"]);

        Assert.Throws<ArgumentException>(() => MetadataSource.Register("MstTakenThief", value, alias is null ? [] : [alias]));
        Assert.False(MetadataSource.TryGet("mst-taken-thief", out _));
        Assert.Same(owner, MetadataSource.Get("mst-taken-owned"));
        Assert.Same(MetadataSource.TMDB, MetadataSource.Get("themoviedb"));
    }

    [Fact]
    public void AMergeRefusesAnAliasOwnedByAnotherSource()
    {
        MetadataSource.Register("MstMergeOwner", "mst-merge-owner", ["mst-merge-owned"]);
        var source = MetadataSource.Register("MstMergeThief", "mst-merge-thief");

        Assert.Throws<ArgumentException>(() => MetadataSource.Register("MstMergeThief", "mst-merge-thief", ["mst-merge-new", "mst-merge-owned"]));

        // Nothing from the refused registration sticks, not even the free alias.
        Assert.Empty(source.Aliases);
        Assert.False(MetadataSource.TryGet("mst-merge-new", out _));
    }

    [Fact]
    public void RegisterRefusesAnAliasThatParseAlreadyHandedOutAsASourceOfItsOwn()
    {
        var handedOut = MetadataSource.Parse("mst-handed-out");

        Assert.Throws<ArgumentException>(() => MetadataSource.Register("MstHandedOutTaker", "mst-handed-out-taker", ["mst-handed-out"]));
        Assert.False(MetadataSource.TryGet("mst-handed-out-taker", out _));
        Assert.Same(handedOut, MetadataSource.Parse("mst-handed-out"));
    }

    [Fact]
    public void AMergeRefusesAnAliasThatParseAlreadyHandedOutAsASourceOfItsOwn()
    {
        MetadataSource.Parse("mst-merge-handed-out");
        var source = MetadataSource.Register("MstMergeHandedOutTaker", "mst-merge-handed-out-taker");

        Assert.Throws<ArgumentException>(() => MetadataSource.Register("MstMergeHandedOutTaker", "mst-merge-handed-out-taker", ["MST-MERGE-HANDED-OUT"]));
        Assert.Empty(source.Aliases);
    }

    [Fact]
    public void RegisterAcceptsANameThatParseAlreadyHandedOutAsASourceOfItsOwn()
    {
        // The name is never input, so it can't shadow the source Parse handed out.
        var handedOut = MetadataSource.Parse("mstnamehandedout");

        MetadataSource.Register("MstNameHandedOut", "mst-name-handed-out");

        Assert.Same(handedOut, MetadataSource.Parse("mstnamehandedout"));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void RegisterRefusesToChangeTheLocalFlag(bool first, bool second)
    {
        var value = $"mst-local-flag-{first}-{second}".ToLowerInvariant();
        var source = MetadataSource.Register("MstLocalFlag" + first + second, value, local: first);

        Assert.Throws<ArgumentException>(() => MetadataSource.Register("MstLocalFlag" + first + second, value, local: second));
        Assert.Equal(first, source.IsLocal);
    }

    public static TheoryData<string, string, string[], bool> CoreChanges() => new()
    {
        { "AniDB", "anidb", ["mst-core-alias"], false },
        { "AniDB", "anidb", [], true },
        { "Shoko", "shoko", [], false },
        { "Locally Generated", "generated", ["mst-core-alias"], true },
    };

    [Theory]
    [MemberData(nameof(CoreChanges))]
    public void RegisterRefusesAnyChangeToACoreSource(string name, string value, string[] aliases, bool local)
    {
        var core = MetadataSource.Get(value);
        var before = core.Aliases;

        Assert.Throws<ArgumentException>(() => MetadataSource.Register(name, value, aliases, local));
        Assert.Equal(before, core.Aliases);
        Assert.False(MetadataSource.TryGet("mst-core-alias", out _));
    }

    [Fact]
    public void RepeatingACoreRegistrationReturnsTheCoreSource()
    {
        Assert.Same(MetadataSource.AniDB, MetadataSource.Register("AniDb", "anidb"));
        Assert.Equal("AniDB", MetadataSource.AniDB.Name);
        Assert.Same(MetadataSource.TMDB, MetadataSource.Register("TMDB", "tmdb"));
        Assert.Same(MetadataSource.TMDB, MetadataSource.Register("TMDB", "tmdb", ["TheMovieDB"]));
        Assert.Same(MetadataSource.Shoko, MetadataSource.Register("Shoko", "shoko", local: true));
        Assert.Same(MetadataSource.Generated, MetadataSource.Register("Locally Generated", "generated", ["LocallyGenerated"], local: true));
    }

    #endregion

    #region Exact Values

    [Fact]
    public void GetByValueSkipsAliases()
    {
        var registered = MetadataSource.Register("MstExact", "mst-exact", ["mst-exact-alias"]);
        Assert.Same(registered, MetadataSource.GetByValue("mst-exact"));

        var exact = MetadataSource.GetByValue("mst-exact-alias");

        Assert.Equal("mst-exact-alias", exact.Value);
        Assert.False(exact.IsRegistered);
        Assert.Same(exact, MetadataSource.GetByValue("mst-exact-alias"));
    }

    [Theory]
    [InlineData("AniDB")]
    [InlineData("themoviedb ")]
    [InlineData("")]
    public void GetByValueRefusesAnythingButAValue(string text)
        => Assert.Throws<ArgumentException>(() => MetadataSource.GetByValue(text));

    #endregion

    #region Parse

    [Theory]
    [InlineData("anidb")]
    [InlineData("AniDB")]
    [InlineData("TheMovieDB")]
    [InlineData(" user ")]
    public void ParseGivesTheRegisteredInstanceForRegisteredText(string text)
    {
        Assert.Same(MetadataSource.Get(text), MetadataSource.Parse(text));
        Assert.True(MetadataSource.TryParse(text, out var source));
        Assert.Same(MetadataSource.Get(text), source);
    }

    [Fact]
    public void ParseGivesAnUnregisteredSourceForAnyOtherValidValue()
    {
        var source = MetadataSource.Parse("mst-parse-some-thing");

        Assert.Equal("mst-parse-some-thing", source.Value);
        Assert.Equal("MstParseSomeThing", source.Name);
        Assert.Empty(source.Aliases);
        Assert.False(source.IsLocal);
        Assert.True(source.IsRemote);
        Assert.False(source.IsRegistered);
        Assert.DoesNotContain(source, MetadataSource.All);
    }

    [Fact]
    public void ParseCapitalizesOnlyTheFirstCharacterOfEachPart()
        => Assert.Equal("Mst9parseXYz", MetadataSource.Parse("mst-9parse-x-yz").Name);

    [Fact]
    public void ParseGivesTheSameUnregisteredInstanceEveryTime()
    {
        var first = MetadataSource.Parse("mst-parse-repeat");

        Assert.Same(first, MetadataSource.Parse("mst-parse-repeat"));
        Assert.Same(first, MetadataSource.Parse(" MST-Parse-Repeat "));
        Assert.True(MetadataSource.TryParse("mst-parse-repeat", out var third));
        Assert.Same(first, third);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("mst parse bad")]
    [InlineData("mst_parse_bad_")]
    [InlineData("_mst_parse_bad")]
    [InlineData("mst__parse_bad")]
    [InlineData("mst.parse.bad")]
    [InlineData("-mst-parse-bad")]
    [InlineData("mst-parse-bad-")]
    [InlineData("mst--parse-bad")]
    [InlineData("mst-parse-bad!")]
    [InlineData("mst-parse-bad-ä")]
    public void ParseRefusesInvalidText(string text)
    {
        Assert.Throws<FormatException>(() => MetadataSource.Parse(text));
        Assert.False(MetadataSource.TryParse(text, out var source));
        Assert.Null(source);
    }

    [Fact]
    public void ParseRefusesAValueThatIsTooLong()
    {
        var text = "mst-parse-long-" + new string('a', MetadataSource.MaxValueLength);

        Assert.Throws<FormatException>(() => MetadataSource.Parse(text));
        Assert.False(MetadataSource.TryParse(text, out _));
    }

    [Fact]
    public void AnUnregisteredSourceReadsThroughALaterRegistration()
    {
        var early = MetadataSource.Parse("mst-late");
        Assert.Equal("MstLate", early.Name);

        var registered = MetadataSource.Register("MstLateRegistered", "mst-late", ["mst-late-alias"], local: true);

        Assert.NotSame(early, registered);
        Assert.Equal(registered, early);
        Assert.Equal("MstLateRegistered", early.Name);
        Assert.Equal(["mst-late-alias"], early.Aliases);
        Assert.True(early.IsLocal);
        Assert.False(early.IsRemote);
        Assert.True(early.IsRegistered);

        Assert.Same(registered, MetadataSource.Parse("mst-late"));
        Assert.Same(registered, MetadataSource.Get("mst-late"));
    }

    [Fact]
    public void AnUnregisteredSourceSeesAliasesMergedIntoItsRegistration()
    {
        var early = MetadataSource.Parse("mst-late-merge");
        MetadataSource.Register("MstLateMerge", "mst-late-merge");
        MetadataSource.Register("MstLateMerge", "mst-late-merge", ["mst-late-merge-alias"]);

        Assert.Equal(["mst-late-merge-alias"], early.Aliases);
    }

    [Fact]
    public async Task ParseAndRegisterNeverBothClaimTheSameText()
    {
        // A text is either an alias of a registered source or a source of its own, never both,
        // however a parse and a registration of it interleave.
        var token = TestContext.Current.CancellationToken;
        for (var i = 0; i < 50; i++)
        {
            var alias = $"mst-race-alias-{i}";
            MetadataSource? parsed = null;
            MetadataSource? registered = null;
            using var barrier = new Barrier(2);
            var parse = Task.Run(() =>
            {
                barrier.SignalAndWait(token);
                parsed = MetadataSource.Parse(alias);
            }, token);
            var register = Task.Run(() =>
            {
                barrier.SignalAndWait(token);
                try
                {
                    registered = MetadataSource.Register($"MstRaceOwner{i}", $"mst-race-owner-{i}", [alias]);
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
    public void SourcesWithTheSameValueAreEqual()
    {
        var registered = MetadataSource.Register("MstEqual", "mst-equal");
        var early = MetadataSource.Parse("mst-equal-early");
        var registeredEarly = MetadataSource.Register("MstEqualEarly", "mst-equal-early");

        Assert.True(registered.Equals(MetadataSource.Get("mst-equal")));
        Assert.True(early.Equals(registeredEarly));
        Assert.True(early.Equals((object)registeredEarly));
        Assert.True(early == registeredEarly);
        Assert.False(early != registeredEarly);
        Assert.Equal(early.GetHashCode(), registeredEarly.GetHashCode());
    }

    [Fact]
    public void ADifferentValueOrNullIsNotEqual()
    {
        MetadataSource? none = null;

        Assert.True(MetadataSource.AniDB != MetadataSource.TMDB);
        Assert.False(MetadataSource.AniDB.Equals((object)"anidb"));
        Assert.False(MetadataSource.AniDB == none);
        Assert.True(none == null);
    }

    [Fact]
    public void SourcesWorkAsDictionaryKeysAcrossInstances()
    {
        var early = MetadataSource.Parse("mst-dictionary-key");
        var dictionary = new Dictionary<MetadataSource, int> { [early] = 1 };
        var registered = MetadataSource.Register("MstDictionaryKey", "mst-dictionary-key");

        Assert.Equal(1, dictionary[registered]);
    }

    [Fact]
    public void SortingPutsOldSourcesFirstThenNewOnesThenPluginAndTheLocalOnes()
    {
        var newer = MetadataSource.Parse("mst-sort-newer");
        var sorted = new[]
        {
            MetadataSource.Shoko,
            MetadataSource.User,
            MetadataSource.Generated,
            MetadataSource.Parse("plugin"),
            newer,
            TestSources.Plugin,
            MetadataSource.Parse("simkl"),
            TestSources.AniList,
            MetadataSource.TMDB,
            MetadataSource.AniDB,
        }.Order().ToList();

        Assert.True(MetadataSource.AniDB.CompareTo(null) > 0);
        Assert.Equal(
            [
                MetadataSource.AniDB,
                MetadataSource.TMDB,
                TestSources.AniList,
                MetadataSource.Parse("simkl"),
                newer,
                TestSources.Plugin,
                MetadataSource.Parse("plugin"),
                MetadataSource.Generated,
                MetadataSource.User,
                MetadataSource.Shoko,
            ],
            sorted
        );
    }

    [Fact]
    public void SortingIgnoresTheName()
    {
        var first = MetadataSource.Register("MstSortZ", "mst-sort-a");
        var second = MetadataSource.Register("MstSortA", "mst-sort-b");

        Assert.Equal([first, second], new[] { second, first }.Order());
    }

    #endregion
}
