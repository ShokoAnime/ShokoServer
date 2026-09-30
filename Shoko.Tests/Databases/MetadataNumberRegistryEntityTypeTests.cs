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
/// Covers the entity type half of <see cref="MetadataNumberRegistry"/>, which
/// maps each <see cref="MetadataEntityType"/> to the one-byte number its
/// database columns store.
/// </summary>
/// <remarks>
/// The old enum's numbers are fixed for good, and so are those of the core
/// kinds added since. Any other kind is handed the lowest free number when it
/// registers, and never at any other time. Other tests register kinds in
/// parallel, so these never assume which free numbers are left, only that the
/// lowest one is used.
/// </remarks>
[Collection(nameof(MetadataNumberRegistryCollection))]
public class MetadataNumberRegistryEntityTypeTests
{
    private const byte FirstFreeNumber = 0x11;

    private const byte LastFreeNumber = 0x7F;

    public static TheoryData<string> FixedEntityTypes()
        => new(MetadataNumberRegistry.FixedEntityTypeNumbers.Keys.Order(StringComparer.Ordinal));

    private static byte Fixed(string value)
        => MetadataNumberRegistry.FixedEntityTypeNumbers[value];

    private static void AssertLowestFree(byte number)
    {
        Assert.InRange(number, FirstFreeNumber, LastFreeNumber);
        for (var below = FirstFreeNumber; below < number; below++)
            Assert.True(MetadataNumberRegistry.IsKnownEntityType(below), $"{below} was free, but {number} was handed out.");
    }

    private static Dictionary<string, byte> ReadFile(string path)
        => JObject.Parse(File.ReadAllText(path))["EntityTypes"]?.ToObject<Dictionary<string, byte>>() ?? [];

    private static MetadataEntityType Register(string value)
        => MetadataEntityType.Register(value, value);

    #region Fixed Numbers

    [Theory]
    [MemberData(nameof(FixedEntityTypes))]
    public void AFixedKindKeepsItsNumberBothWaysAndHasNoProblem(string value)
    {
        using var scope = new MetadataNumberRegistryScope();
        var number = Fixed(value);

        var entityType = MetadataNumberRegistry.GetEntityType(number);

        Assert.Equal(number, MetadataNumberRegistry.GetNumber(MetadataEntityType.Parse(value)));
        Assert.Equal(value, entityType.Value);
        if (entityType.IsRegistered)
            Assert.Same(MetadataEntityType.Get(value), entityType);
        Assert.True(MetadataNumberRegistry.IsKnownEntityType(number));
        Assert.Null(MetadataNumberRegistry.FindEntityTypeProblem(number));
        // Never handed out to another kind.
        Assert.False(number is >= FirstFreeNumber and <= LastFreeNumber);
    }

    [Theory]
    // Numbers no kind is ever handed: below the free range and above the signed byte range.
    [InlineData(0)]
    [InlineData(200)]
    [InlineData(255)]
    public void AnUnknownNumberReadsAsAnUnregisteredKindAndStoresAsItselfAgain(byte number)
    {
        using var scope = new MetadataNumberRegistryScope();

        var entityType = MetadataNumberRegistry.GetEntityType(number);

        Assert.Equal($"unknown-{number}", entityType.Value);
        Assert.False(entityType.IsRegistered);
        Assert.Equal(number, MetadataNumberRegistry.GetNumber(entityType));
    }

    [Fact]
    public void AnUnknownValueNamingAFixedNumberCanNotBeStored()
        => Assert.Throws<InvalidOperationException>(() => MetadataNumberRegistry.GetNumber(MetadataEntityType.Parse($"unknown-{Fixed("series")}")));

    #endregion

    #region Handing Out Numbers

    [Fact]
    public void ARegistrationGetsTheLowestFreeNumberRightAway()
    {
        using var scope = new MetadataNumberRegistryScope();

        var entityType = Register("mnret-on-register");
        var number = MetadataNumberRegistry.GetNumber(entityType);

        AssertLowestFree(number);
        Assert.Equal(number, ReadFile(scope.FilePath)["mnret-on-register"]);
        Assert.Same(entityType, MetadataNumberRegistry.GetEntityType(number));
        Assert.False(File.Exists(scope.FilePath + ".tmp"));
    }

    [Fact]
    public void LoadingGivesEveryRegisteredKindANumber()
    {
        var entityType = Register("mnret-before-load");
        _ = TestEntityTypes.Library;

        using var scope = new MetadataNumberRegistryScope();

        var file = ReadFile(scope.FilePath);
        Assert.Contains(entityType.Value, file.Keys);
        Assert.DoesNotContain("library", file.Keys);
        Assert.Equal(Fixed("library"), MetadataNumberRegistry.GetNumber(TestEntityTypes.Library));
    }

    [Fact]
    public void AnUnregisteredKindIsNeverHandedANumber()
    {
        using var scope = new MetadataNumberRegistryScope();

        var entityType = MetadataEntityType.Parse("mnret-unregistered");

        Assert.Throws<InvalidOperationException>(() => MetadataNumberRegistry.GetNumber(entityType));
        Assert.DoesNotContain("mnret-unregistered", ReadFile(scope.FilePath).Keys);
    }

    [Fact]
    public void ADormantKindKeepsTheNumberTheFileGaveIt()
    {
        using var scope = new MetadataNumberRegistryScope("""{ "Sources": {}, "EntityTypes": { "mnret-dormant": 112 } }""");

        var entityType = MetadataNumberRegistry.GetEntityType(112);

        Assert.Equal("mnret-dormant", entityType.Value);
        Assert.False(entityType.IsRegistered);
        Assert.Equal(112, MetadataNumberRegistry.GetNumber(entityType));
        Assert.Equal(112, MetadataNumberRegistry.GetNumber(MetadataEntityType.Parse("MNRET-DORMANT")));
    }

    [Fact]
    public void AKindRegisteredLaterKeepsTheNumberTheFileGaveIt()
    {
        using var scope = new MetadataNumberRegistryScope("""{ "Sources": {}, "EntityTypes": { "mnret-later": 113 } }""");

        var entityType = Register("mnret-later");

        Assert.Equal(113, MetadataNumberRegistry.GetNumber(entityType));
        Assert.Equal(113, ReadFile(scope.FilePath)["mnret-later"]);
    }

    [Fact]
    public void AMergedRegistrationKeepsItsNumber()
    {
        using var scope = new MetadataNumberRegistryScope();
        var entityType = Register("mnret-merged");
        var number = MetadataNumberRegistry.GetNumber(entityType);

        MetadataEntityType.Register("mnret-merged", "mnret-merged", ["mnret-merged-alias"]);

        Assert.Equal(number, MetadataNumberRegistry.GetNumber(entityType));
    }

    [Fact]
    public void NumbersSurviveAReload()
    {
        using var scope = new MetadataNumberRegistryScope();
        var number = MetadataNumberRegistry.GetNumber(Register("mnret-reload"));

        scope.Load();

        Assert.Equal(number, MetadataNumberRegistry.GetNumber(MetadataEntityType.Parse("mnret-reload")));
        Assert.Equal("mnret-reload", MetadataNumberRegistry.GetEntityType(number).Value);
    }

    #endregion

    #region The File

    [Fact]
    public void SourcesAndEntityTypesKeepSeparateNumbers()
    {
        using var scope = new MetadataNumberRegistryScope("""{ "Sources": { "mnret-same": 115 }, "EntityTypes": { "mnret-same": 115 } }""");

        Assert.Equal("mnret-same", MetadataNumberRegistry.GetSource(115).Value);
        Assert.Equal("mnret-same", MetadataNumberRegistry.GetEntityType(115).Value);
    }

    [Fact]
    public void TheFileNeverHoldsAFixedKind()
    {
        var season = Fixed("season");
        using var scope = new MetadataNumberRegistryScope(
            $$"""{ "Sources": {}, "EntityTypes": { "series": 116, "library": 117, "ordering": 120, "mnret-on-fixed": {{season}} } }"""
        );

        Assert.Equal(Fixed("series"), MetadataNumberRegistry.GetNumber(MetadataEntityType.Series));
        Assert.Equal(Fixed("ordering"), MetadataNumberRegistry.GetNumber(MetadataEntityType.Ordering));
        Assert.NotEqual("series", MetadataNumberRegistry.GetEntityType(116).Value);
        Assert.NotEqual("library", MetadataNumberRegistry.GetEntityType(117).Value);
        Assert.Same(MetadataEntityType.Season, MetadataNumberRegistry.GetEntityType(season));

        Register("mnret-no-fixed");
        var file = ReadFile(scope.FilePath);
        Assert.DoesNotContain("series", file.Keys);
        Assert.DoesNotContain("library", file.Keys);
        Assert.DoesNotContain("ordering", file.Keys);
        Assert.DoesNotContain("mnret-on-fixed", file.Keys);
        Assert.All(file.Values, number => Assert.InRange(number, FirstFreeNumber, LastFreeNumber));
    }

    [Fact]
    public void ASecondFileEntryOnATakenNumberOrAnInvalidValueIsIgnored()
    {
        using var scope = new MetadataNumberRegistryScope("""{ "Sources": {}, "EntityTypes": { "mnret-taken-first": 118, "mnret-taken-second": 118, "Mnret Invalid": 119 } }""");

        Assert.Equal("mnret-taken-first", MetadataNumberRegistry.GetEntityType(118).Value);
        Assert.DoesNotContain("Mnret Invalid", ReadFile(scope.FilePath).Keys);
        Assert.Throws<InvalidOperationException>(() => MetadataNumberRegistry.GetNumber(MetadataEntityType.Parse("mnret-taken-second")));
    }

    [Fact]
    public void AFileEntryAboveTheSignedByteRangeIsIgnored()
    {
        using var scope = new MetadataNumberRegistryScope("""{ "Sources": {}, "EntityTypes": { "mnret-too-high": 200 } }""");

        Assert.False(MetadataNumberRegistry.IsKnownEntityType(200));
        Assert.Equal("unknown-200", MetadataNumberRegistry.GetEntityType(200).Value);
    }

    [Fact]
    public void FewFreeNumbersLeftIsWarnedAboutApartFromSources()
    {
        // Leaves some room for the kinds other tests register in parallel.
        var registered = MetadataEntityType.All.Count(entityType => !MetadataNumberRegistry.FixedEntityTypeNumbers.ContainsKey(entityType.Value));
        var fill = LastFreeNumber - FirstFreeNumber + 1 - registered - (MetadataNumberRegistry.LowFreeNumberCount - 8);
        var entityTypes = Enumerable.Range(LastFreeNumber - fill + 1, fill).ToDictionary(number => $"mnret-low-{number}", number => (byte)number);

        using (var scope = new MetadataNumberRegistryScope(new JObject { ["Sources"] = new JObject(), ["EntityTypes"] = JObject.FromObject(entityTypes) }.ToString()))
        {
            Assert.True(MetadataNumberRegistry.HasWarnedAboutFreeEntityTypeNumbers);
            Assert.False(MetadataNumberRegistry.HasWarnedAboutFreeNumbers);
        }

        using var fresh = new MetadataNumberRegistryScope();
        Assert.False(MetadataNumberRegistry.HasWarnedAboutFreeEntityTypeNumbers);
    }

    [Fact]
    public void AFileWithANullEntityTypeSectionIsUnreadable()
    {
        using var scope = new MetadataNumberRegistryScope("""{ "Sources": {}, "EntityTypes": null }""");

        var entityType = Register("mnret-null-section");

        Assert.Throws<InvalidOperationException>(() => MetadataNumberRegistry.GetNumber(entityType));
    }

    [Fact]
    public void AnUnreadableFileHandsOutNoNewNumber()
    {
        using var scope = new MetadataNumberRegistryScope("not json");

        var entityType = Register("mnret-unreadable");

        Assert.Throws<InvalidOperationException>(() => MetadataNumberRegistry.GetNumber(entityType));
        Assert.Equal(Fixed("series"), MetadataNumberRegistry.GetNumber(MetadataEntityType.Series));
        Assert.Equal("not json", File.ReadAllText(scope.FilePath));
    }

    #endregion

    #region Stored Number Problems

    [Fact]
    public void ANumberNamingNoKindIsAProblem()
    {
        using var scope = new MetadataNumberRegistryScope("""{ "Sources": {} }""");

        // Above the signed byte range, so no kind is ever handed it.
        Assert.NotNull(MetadataNumberRegistry.FindEntityTypeProblem(200));
    }

    [Fact]
    public void AStoredKindThatIsNowAnAliasIsAProblem()
    {
        using var scope = new MetadataNumberRegistryScope("""{ "Sources": {}, "EntityTypes": { "mnret-became-alias": 114 } }""");
        MetadataEntityType.Register("MnretAliasOwner", "mnret-alias-owner", ["mnret-became-alias"]);

        Assert.Equal("mnret-became-alias", MetadataNumberRegistry.GetEntityType(114).Value);
        Assert.Contains("now an alias of \"mnret-alias-owner\"", MetadataNumberRegistry.FindEntityTypeProblem(114));
    }

    [Fact]
    public void ARegisteredPluginKindHasNoProblem()
    {
        using var scope = new MetadataNumberRegistryScope();
        var number = MetadataNumberRegistry.GetNumber(Register("mnret-no-problem"));

        Assert.Null(MetadataNumberRegistry.FindEntityTypeProblem(number));
    }

    #endregion
}
