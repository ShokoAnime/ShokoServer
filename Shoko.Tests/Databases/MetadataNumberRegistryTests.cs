using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Databases;

/// <summary>
/// Covers <see cref="MetadataNumberRegistry"/>, which maps each source to the
/// one-byte number its database columns store.
/// </summary>
/// <remarks>
/// A number that changes meaning between two runs points every stored row at
/// the wrong source, with no error anywhere, so the fixed numbers and the file
/// that keeps the handed-out ones are what these guard. Only a registered
/// source is handed a new number. Other tests register sources in parallel,
/// so these never assume which free numbers are left, only that the lowest
/// one is used.
/// </remarks>
[Collection(nameof(MetadataNumberRegistryCollection))]
public class MetadataNumberRegistryTests
{
    private const byte FirstFreeNumber = 0x0E;

    private const byte LastFreeNumber = 0xFA;

    public static TheoryData<string> FixedSources()
        => new(MetadataNumberRegistry.FixedSourceNumbers.Keys.Order(StringComparer.Ordinal));

    private static byte Fixed(string value)
        => MetadataNumberRegistry.FixedSourceNumbers[value];

    private static void AssertLowestFree(byte number)
    {
        Assert.InRange(number, FirstFreeNumber, LastFreeNumber);
        for (var below = FirstFreeNumber; below < number; below++)
            Assert.True(MetadataNumberRegistry.IsKnown(below), $"{below} was free, but {number} was handed out.");
    }

    private static Dictionary<string, byte> ReadFile(string path)
        => JObject.Parse(File.ReadAllText(path))["Sources"]!.ToObject<Dictionary<string, byte>>()!;

    private static MetadataSource Register(string value)
        => MetadataSource.Register(value, value);

    #region Fixed Numbers

    [Theory]
    [MemberData(nameof(FixedSources))]
    public void AFixedSourceKeepsItsNumberBothWaysAndTheMigrationsKnowItsOldSpelling(string value)
    {
        var number = Fixed(value);

        var source = MetadataNumberRegistry.GetSource(number);

        Assert.Equal(number, MetadataNumberRegistry.GetNumber(MetadataSource.Parse(value)));
        Assert.Equal(value, source.Value);
        if (source.IsRegistered)
            Assert.Same(MetadataSource.Get(value), source);
        Assert.True(MetadataNumberRegistry.IsKnown(number));
        Assert.Null(MetadataNumberRegistry.FindProblem(number));
        // Never handed out to another source.
        Assert.False(number is >= FirstFreeNumber and <= LastFreeNumber);
        var oldSpelling = DatabaseFixes.GetOldSourceSpelling(value);
        Assert.NotNull(oldSpelling);
        Assert.Equal(value, DatabaseFixes.ConvertOldSourceName(oldSpelling));
    }

    [Fact]
    public void AnUnknownNumberReadsAsAnUnregisteredSource()
    {
        // 0xFD sits outside the range numbers are handed out from, so it can never be assigned.
        var source = MetadataNumberRegistry.GetSource(0xFD);

        Assert.Equal("unknown-253", source.Value);
        Assert.False(source.IsRegistered);
        Assert.False(MetadataNumberRegistry.IsKnown(0xFD));
        Assert.NotNull(MetadataNumberRegistry.FindProblem(0xFD));
    }

    [Fact]
    public void ANewSourceHasNoOldSpelling()
    {
        Assert.Null(DatabaseFixes.GetOldSourceSpelling(TestSources.Plugin.Value));
        Assert.Null(DatabaseFixes.GetOldSourceSpelling("mnr-new-spelling"));
    }

    #endregion

    #region Handing Out Numbers

    [Fact]
    public void ANewSourceGetsTheLowestFreeNumber()
    {
        using var scope = new MetadataNumberRegistryScope();

        var first = MetadataNumberRegistry.GetNumber(MetadataSource.Register("MnrNewFirst", "mnr-new-first"));
        AssertLowestFree(first);
        var second = MetadataNumberRegistry.GetNumber(Register("mnr-new-second"));
        AssertLowestFree(second);

        Assert.NotEqual(first, second);
        Assert.Equal("mnr-new-first", MetadataNumberRegistry.GetSource(first).Value);
        Assert.Equal("mnr-new-second", MetadataNumberRegistry.GetSource(second).Value);
    }

    [Fact]
    public void ASourceKeepsItsNumber()
    {
        using var scope = new MetadataNumberRegistryScope();
        var source = Register("mnr-keeps");

        var number = MetadataNumberRegistry.GetNumber(source);

        Assert.Equal(number, MetadataNumberRegistry.GetNumber(source));
        Assert.Equal(number, MetadataNumberRegistry.GetNumber(MetadataSource.Parse("mnr-keeps")));
    }

    [Fact]
    public void ARegistrationGetsANumberRightAway()
    {
        using var scope = new MetadataNumberRegistryScope();

        MetadataSource.Register("MnrOnRegister", "mnr-on-register");

        Assert.Contains("mnr-on-register", ReadFile(scope.FilePath).Keys);
    }

    [Fact]
    public void LoadingGivesEveryRegisteredSourceANumber()
    {
        var source = MetadataSource.Register("MnrBeforeLoad", "mnr-before-load");

        using var scope = new MetadataNumberRegistryScope();

        Assert.Contains(source.Value, ReadFile(scope.FilePath).Keys);
        Assert.Contains(TestSources.Plugin.Value, ReadFile(scope.FilePath).Keys);
    }

    [Fact]
    public void AGapInTheFileIsFilledFirst()
    {
        // The file takes every number up to 60 but 30; loading hands out numbers to the registered
        // sources, which always include the test plugin, so the gap is the first to go.
        _ = TestSources.Plugin;
        var sources = new Dictionary<string, byte>();
        for (byte number = FirstFreeNumber; number <= 60; number++)
        {
            if (number is not 30)
                sources[$"mnr-gap-{number}"] = number;
        }

        using var scope = new MetadataNumberRegistryScope(new JObject { ["Sources"] = JObject.FromObject(sources) }.ToString());

        Assert.True(MetadataNumberRegistry.IsKnown(30));
        Assert.DoesNotContain("mnr-gap-", MetadataNumberRegistry.GetSource(30).Value);
        var next = MetadataNumberRegistry.GetNumber(Register("mnr-gap-next"));
        Assert.True(next > 60);
        AssertLowestFree(next);
    }

    #endregion

    #region Unregistered Sources

    [Fact]
    public void AnUnregisteredSourceWithoutANumberCanNotBeStored()
    {
        using var scope = new MetadataNumberRegistryScope();
        var source = MetadataSource.Parse("mnr-unregistered");

        Assert.Throws<InvalidOperationException>(() => MetadataNumberRegistry.GetNumber(source));
        Assert.DoesNotContain("mnr-unregistered", ReadFile(scope.FilePath).Keys);
    }

    [Fact]
    public void AnUnregisteredSourceKeepsTheNumberTheFileGaveIt()
    {
        using var scope = new MetadataNumberRegistryScope("""{ "Sources": { "mnr-dormant": 212 } }""");

        var source = MetadataNumberRegistry.GetSource(212);

        Assert.Equal("mnr-dormant", source.Value);
        Assert.False(source.IsRegistered);
        Assert.Equal(212, MetadataNumberRegistry.GetNumber(source));
    }

    [Theory]
    [InlineData(0xFA)]
    [InlineData(0xF0)]
    public void AnUnknownNumberStoresAsItselfAgain(byte number)
    {
        using var scope = new MetadataNumberRegistryScope("""{ "Sources": {} }""");
        Assert.SkipWhen(MetadataNumberRegistry.IsKnown(number), $"The sources registered so far took {number}.");

        var source = MetadataNumberRegistry.GetSource(number);

        Assert.Equal(number, MetadataNumberRegistry.GetNumber(source));
        Assert.Equal(number, MetadataNumberRegistry.GetNumber(MetadataSource.Parse($"unknown-{number}")));
        Assert.False(MetadataNumberRegistry.IsKnown(number));
    }

    [Theory]
    [MemberData(nameof(FixedSources))]
    public void AnUnknownValueNamingAFixedNumberCanNotBeStored(string value)
    {
        using var scope = new MetadataNumberRegistryScope();

        // Stored as that number, the row would read back as the source that has it.
        Assert.Throws<InvalidOperationException>(() => MetadataNumberRegistry.GetNumber(MetadataSource.Parse($"unknown-{Fixed(value)}")));
    }

    [Fact]
    public void AnUnknownValueNamingAnAssignedNumberCanNotBeStored()
    {
        using var scope = new MetadataNumberRegistryScope("""{ "Sources": { "mnr-assigned": 214 } }""");

        Assert.Throws<InvalidOperationException>(() => MetadataNumberRegistry.GetNumber(MetadataSource.Parse("unknown-214")));
    }

    [Theory]
    [InlineData("unknown-0253")]
    [InlineData("unknown-256")]
    public void OnlyTheExactUnknownSpellingStoresAsItsNumber(string value)
    {
        using var scope = new MetadataNumberRegistryScope();

        Assert.Throws<InvalidOperationException>(() => MetadataNumberRegistry.GetNumber(MetadataSource.Parse(value)));
    }

    #endregion

    #region Looking Up Without Handing Out

    [Fact]
    public void AFixedOrAssignedSourceIsFoundWithoutAWrite()
    {
        using var scope = new MetadataNumberRegistryScope();
        var source = Register("mnr-try-assigned");
        var number = MetadataNumberRegistry.GetNumber(source);

        Assert.True(MetadataNumberRegistry.TryGetNumber(MetadataSource.AniDB, out var anidb));
        Assert.Equal(Fixed("anidb"), anidb);
        Assert.True(MetadataNumberRegistry.TryGetNumber(source, out var found));
        Assert.Equal(number, found);
    }

    [Fact]
    public void AnUnregisteredSourceWithoutANumberIsNotGivenOne()
    {
        using var scope = new MetadataNumberRegistryScope();
        var source = MetadataSource.Parse("mnr-try-unregistered");

        Assert.False(MetadataNumberRegistry.TryGetNumber(source, out _));
        Assert.DoesNotContain("mnr-try-unregistered", ReadFile(scope.FilePath).Keys);
    }

    [Fact]
    public void AnUnreadableFileMakesTheLookupFailWithoutAThrow()
    {
        using var scope = new MetadataNumberRegistryScope("not json");
        var source = Register("mnr-try-unreadable");

        Assert.False(MetadataNumberRegistry.TryGetNumber(source, out _));
        Assert.Equal("not json", File.ReadAllText(scope.FilePath));
    }

    [Theory]
    [InlineData("unknown-253", true)]
    [InlineData("unknown-5", false)]
    public void AnUnknownValueIsFoundOnlyWhenItsNumberIsFree(string value, bool found)
    {
        using var scope = new MetadataNumberRegistryScope("""{ "Sources": {} }""");

        Assert.Equal(found, MetadataNumberRegistry.TryGetNumber(MetadataSource.Parse(value), out _));
    }

    #endregion

    #region Exact Values

    [Fact]
    public void AStoredValueReadsBackAsItselfEvenWhenItIsNowAnAlias()
    {
        using var scope = new MetadataNumberRegistryScope("""{ "Sources": { "mnr-became-alias": 213 } }""");
        var owner = MetadataSource.Register("MnrAliasOwner", "mnr-alias-owner", ["mnr-became-alias"]);

        var source = MetadataNumberRegistry.GetSource(213);

        Assert.Equal("mnr-became-alias", source.Value);
        Assert.NotEqual(owner, source);
        Assert.Contains("now an alias of \"mnr-alias-owner\"", MetadataNumberRegistry.FindProblem(213));
    }

    [Fact]
    public void AnOldValueReadsBackAsItselfWhateverIsRegistered()
    {
        // Once handed out as a source of its own, no other source can take it as an alias.
        _ = MetadataSource.Parse("animeshon");
        Assert.Throws<ArgumentException>(() => MetadataSource.Register("MnrOldAliasThief", "mnr-old-alias-thief", ["Animeshon"]));

        Assert.Equal("animeshon", MetadataNumberRegistry.GetSource(Fixed("animeshon")).Value);
        Assert.Null(MetadataNumberRegistry.FindProblem(Fixed("animeshon")));
    }

    #endregion

    #region Running Low

    [Fact]
    public void FewFreeNumbersLeftIsWarnedAbout()
    {
        // Leaves some room for the sources other tests register in parallel.
        var registered = MetadataSource.All.Count(source => !MetadataNumberRegistry.FixedSourceNumbers.ContainsKey(source.Value));
        var fill = LastFreeNumber - FirstFreeNumber + 1 - registered - (MetadataNumberRegistry.LowFreeNumberCount - 8);
        var sources = Enumerable.Range(LastFreeNumber - fill + 1, fill).ToDictionary(number => $"mnr-low-{number}", number => (byte)number);

        using (var scope = new MetadataNumberRegistryScope(new JObject { ["Sources"] = JObject.FromObject(sources) }.ToString()))
            Assert.True(MetadataNumberRegistry.HasWarnedAboutFreeNumbers);

        // A fresh folder has plenty again, and frees the numbers for everything else.
        using var fresh = new MetadataNumberRegistryScope();
        Assert.False(MetadataNumberRegistry.HasWarnedAboutFreeNumbers);
    }

    #endregion

    #region The File

    [Fact]
    public void TheFileIsWrittenWhenANumberIsHandedOut()
    {
        using var scope = new MetadataNumberRegistryScope();

        var number = MetadataNumberRegistry.GetNumber(Register("mnr-written"));

        Assert.Equal(number, ReadFile(scope.FilePath)["mnr-written"]);
        Assert.False(File.Exists(scope.FilePath + ".tmp"));
    }

    [Fact]
    public void TheFileNeverHoldsAFixedSource()
    {
        using var scope = new MetadataNumberRegistryScope();

        MetadataNumberRegistry.GetNumber(Register("mnr-no-fixed"));
        MetadataNumberRegistry.GetNumber(MetadataSource.AniDB);

        var file = ReadFile(scope.FilePath);
        Assert.DoesNotContain("anidb", file.Keys);
        Assert.DoesNotContain("shoko", file.Keys);
        Assert.All(file.Values, number => Assert.InRange(number, FirstFreeNumber, LastFreeNumber));
    }

    [Fact]
    public void AnExistingFileIsLoaded()
    {
        using var scope = new MetadataNumberRegistryScope("""{ "Sources": { "mnr-load-a": 210, "mnr-load-b": 211 } }""");

        Assert.Equal("mnr-load-a", MetadataNumberRegistry.GetSource(210).Value);
        Assert.Equal(211, MetadataNumberRegistry.GetNumber(MetadataSource.Parse("mnr-load-b")));
        Assert.True(MetadataNumberRegistry.IsKnown(210));
        Assert.True(MetadataNumberRegistry.IsKnown(211));

        var number = MetadataNumberRegistry.GetNumber(Register("mnr-load-c"));
        Assert.NotEqual(210, number);
        Assert.NotEqual(211, number);
    }

    [Fact]
    public void NumbersSurviveAReload()
    {
        using var scope = new MetadataNumberRegistryScope();
        var number = MetadataNumberRegistry.GetNumber(Register("mnr-reload"));

        scope.Load();

        Assert.Equal(number, MetadataNumberRegistry.GetNumber(MetadataSource.Parse("mnr-reload")));
        Assert.Equal("mnr-reload", MetadataNumberRegistry.GetSource(number).Value);
    }

    [Fact]
    public void LoadingForgetsWhatTheLastFolderAssigned()
    {
        byte number;
        using (var first = new MetadataNumberRegistryScope("""{ "Sources": { "mnr-forget": 220 } }"""))
            number = MetadataNumberRegistry.GetNumber(MetadataSource.Parse("mnr-forget"));

        using var second = new MetadataNumberRegistryScope("""{ "Sources": { "mnr-forget-other": 220 } }""");

        // Unregistered, the source had only the number the first file gave it.
        Assert.Equal(220, number);
        Assert.Equal("mnr-forget-other", MetadataNumberRegistry.GetSource(220).Value);
        Assert.Throws<InvalidOperationException>(() => MetadataNumberRegistry.GetNumber(MetadataSource.Parse("mnr-forget")));
    }

    [Fact]
    public void AFileEntryOnAFixedNumberIsIgnored()
    {
        var (kitsu, plugin, shoko) = (Fixed("kitsu"), Fixed("plugin"), Fixed("shoko"));
        var sources = new Dictionary<string, byte> { ["mnr-on-fixed"] = kitsu, ["mnr-on-plugin"] = plugin, ["mnr-on-shoko"] = shoko };
        using var scope = new MetadataNumberRegistryScope(new JObject { ["Sources"] = JObject.FromObject(sources) }.ToString());

        Assert.Equal("kitsu", MetadataNumberRegistry.GetSource(kitsu).Value);
        Assert.Equal("plugin", MetadataNumberRegistry.GetSource(plugin).Value);
        Assert.Same(MetadataSource.Shoko, MetadataNumberRegistry.GetSource(shoko));
        AssertLowestFree(MetadataNumberRegistry.GetNumber(Register("mnr-on-fixed")));

        var file = ReadFile(scope.FilePath);
        Assert.DoesNotContain("mnr-on-plugin", file.Keys);
        Assert.DoesNotContain("mnr-on-shoko", file.Keys);
        Assert.NotEqual(kitsu, file["mnr-on-fixed"]);
    }

    [Fact]
    public void AFileEntryForAFixedSourceIsIgnored()
    {
        using var scope = new MetadataNumberRegistryScope("""{ "Sources": { "anidb": 230, "shoko": 231 } }""");

        Assert.Equal(Fixed("anidb"), MetadataNumberRegistry.GetNumber(MetadataSource.AniDB));
        Assert.Equal(Fixed("shoko"), MetadataNumberRegistry.GetNumber(MetadataSource.Shoko));
        Assert.NotEqual("anidb", MetadataNumberRegistry.GetSource(230).Value);
        Assert.NotEqual("shoko", MetadataNumberRegistry.GetSource(231).Value);
    }

    [Fact]
    public void ASecondFileEntryOnATakenNumberIsIgnored()
    {
        using var scope = new MetadataNumberRegistryScope("""{ "Sources": { "mnr-taken-first": 240, "mnr-taken-second": 240 } }""");

        Assert.Equal("mnr-taken-first", MetadataNumberRegistry.GetSource(240).Value);
        Assert.NotEqual(240, MetadataNumberRegistry.GetNumber(Register("mnr-taken-second")));
    }

    [Fact]
    public void AFileEntryWithAnInvalidValueIsIgnored()
    {
        // Reading the number back would otherwise fail on every row that holds it.
        using var scope = new MetadataNumberRegistryScope("""{ "Sources": { "Mnr Invalid": 241, "MNR-UPPER": 242 } }""");
        // The file is only rewritten when a number is handed out, which a load does not always do.
        Register("mnr-invalid-rewrite");

        Assert.NotEqual("mnr-upper", MetadataNumberRegistry.GetSource(241).Value);
        Assert.NotEqual("mnr-upper", MetadataNumberRegistry.GetSource(242).Value);
        Assert.DoesNotContain("Mnr Invalid", ReadFile(scope.FilePath).Keys);
        Assert.DoesNotContain("MNR-UPPER", ReadFile(scope.FilePath).Keys);
    }

    [Fact]
    public void AFileEntryOutsideTheFreeRangeIsIgnored()
    {
        using var scope = new MetadataNumberRegistryScope("""{ "Sources": { "mnr-out-of-range": 253 } }""");

        Assert.False(MetadataNumberRegistry.IsKnown(0xFD));
        Assert.NotEqual(0xFD, MetadataNumberRegistry.GetNumber(Register("mnr-out-of-range")));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("null")]
    [InlineData("""{ "Sources": null }""")]
    public void AnUnreadableFileIsKeptAsABadCopyAndLeftAlone(string contents)
    {
        using var scope = new MetadataNumberRegistryScope(contents);

        Assert.Equal(contents, File.ReadAllText(scope.FilePath + ".bad"));
        Assert.Equal(contents, File.ReadAllText(scope.FilePath));
    }

    [Fact]
    public void AnUnreadableFileHandsOutNoNewNumber()
    {
        using var scope = new MetadataNumberRegistryScope("not json");

        // A number handed out now may be one the lost file gave a source whose rows are still stored.
        var source = Register("mnr-unreadable");

        Assert.Throws<InvalidOperationException>(() => MetadataNumberRegistry.GetNumber(source));
        Assert.Equal(Fixed("anidb"), MetadataNumberRegistry.GetNumber(MetadataSource.AniDB));
        Assert.Equal("not json", File.ReadAllText(scope.FilePath));
    }

    [Fact]
    public void AnUnreadableFileIsOnlyKeptOnceWhileItStaysTheSame()
    {
        using var scope = new MetadataNumberRegistryScope("not json");

        scope.Load();
        scope.Load();

        Assert.Equal("not json", File.ReadAllText(scope.FilePath + ".bad"));
        Assert.False(File.Exists(scope.FilePath + ".1.bad"));
    }

    [Fact]
    public void AnUnreadableFileNeverOverwritesAnEarlierBadCopy()
    {
        using var scope = new MetadataNumberRegistryScope(load: false);
        File.WriteAllText(scope.FilePath, "still not json");
        File.WriteAllText(scope.FilePath + ".bad", "not json");

        scope.Load();

        Assert.Equal("not json", File.ReadAllText(scope.FilePath + ".bad"));
        Assert.Equal("still not json", File.ReadAllText(scope.FilePath + ".1.bad"));
    }

    [Fact]
    public void WithoutALoadNumbersAreHandedOutButNotWritten()
    {
        using var scope = new MetadataNumberRegistryScope(load: false);
        MetadataNumberRegistryScope.Unload();

        AssertLowestFree(MetadataNumberRegistry.GetNumber(Register("mnr-not-loaded")));
        Assert.False(File.Exists(scope.FilePath));
    }

    [Fact]
    public void CopyWithBackupCopiesTheFileNextToTheBackup()
    {
        using var scope = new MetadataNumberRegistryScope();
        MetadataNumberRegistry.GetNumber(Register("mnr-backup"));
        var backup = Path.Join(scope.DataPath, "backup.db3");

        MetadataNumberRegistry.CopyWithBackup(backup);

        var copy = backup + "." + MetadataNumberRegistry.FileName;
        Assert.True(File.Exists(copy));
        Assert.Equal(File.ReadAllText(scope.FilePath), File.ReadAllText(copy));
    }

    [Fact]
    public void CopyWithBackupDoesNothingWithoutAFile()
    {
        using var scope = new MetadataNumberRegistryScope(load: false);
        MetadataNumberRegistryScope.Unload();
        var backup = Path.Join(scope.DataPath, "backup.db3");

        MetadataNumberRegistry.CopyWithBackup(backup);

        Assert.False(File.Exists(backup + "." + MetadataNumberRegistry.FileName));
    }

    #endregion
}
