using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Abstractions.Metadata.Text;
using Shoko.Abstractions.Metadata.Text.Options;
using Shoko.Server.API.Resolvers;
using Shoko.Server.API.v3.Controllers;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.TextManagement;
using Shoko.Server.API.v3.Models.TextManagement.Input;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Covers every <c>Text/Management</c> route over a mocked text manager: what
/// each passes to the manager, how it answers a missing text or entry and a
/// refused write, the inline default among an entry's texts, and the step the
/// preferred route reports.
/// </summary>
public class TextManagementControllerTests
{
    #region Fixture

    private static readonly JsonSerializerSettings _apiSettings = new() { ContractResolver = new ApiContractResolver() };

    private static readonly MetadataGuid _anime = new(MetadataSource.AniDB, MetadataEntityType.Series, "1");

    private static readonly MetadataGuid _episode = new(MetadataSource.AniDB, MetadataEntityType.Episode, "10");

    /// <summary>
    /// A stored title, with the members a stored text carries.
    /// </summary>
    private sealed class StoredTitle : TitleStub, ITitle
    {
        public int? ID { get; init; }

        public MetadataGuid? EntityID { get; init; }

        public int? ReferenceID { get; init; }

        public bool IsEnabled { get; init; } = true;

        public TextPreference Preference { get; init; }

        public int Ordering { get; init; }

        public string? ScriptCode { get; init; }

        public bool IsInlineDefault { get; init; }

        public bool IsSynthesized { get; init; }
    }

    /// <summary>
    /// A stored overview, with the members a stored text carries.
    /// </summary>
    private sealed class StoredOverview : TextStub, IText
    {
        public int? ID { get; init; }

        public MetadataGuid? EntityID { get; init; }

        public bool IsEnabled { get; init; } = true;

        public TextPreference Preference { get; init; }

        public bool IsInlineDefault { get; init; }
    }

    private static StoredTitle Title(int? id, string value, TitleLanguage language = TitleLanguage.English, string code = "en", MetadataSource? source = null, TitleType type = TitleType.Official, MetadataGuid? entity = null)
        => new()
        {
            ID = id,
            EntityID = id is null ? null : entity ?? _anime,
            Source = source ?? MetadataSource.AniDB,
            Value = value,
            Language = language,
            LanguageCode = code,
            Type = type,
        };

    private static StoredOverview Overview(int? id, string value, MetadataSource? source = null)
        => new()
        {
            ID = id,
            EntityID = id is null ? null : _anime,
            Source = source ?? MetadataSource.AniDB,
            Value = value,
            Language = TitleLanguage.English,
            LanguageCode = "en",
        };

    /// <summary>
    /// The controller over a mocked text manager and metadata service.
    /// </summary>
    private sealed class Fixture
    {
        public Mock<IMetadataTextManager> Text { get; } = new();

        public Mock<IMetadataService> Metadata { get; } = new();

        public ServerSettings Settings { get; } = new();

        public TextManagementController Controller()
            => new(Text.Object, Metadata.Object, new StubSettingsProvider(Settings))
            {
                ControllerContext = new()
                {
                    HttpContext = new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().AddMvcCore().Services.BuildServiceProvider() },
                },
            };

        public void Stores(MetadataGuid id)
            => Metadata.Setup(m => m.GetEntry(id)).Returns(Mock.Of<IMetadata>(entry => entry.ID == id));
    }

    private static T Value<T>(ActionResult<T> result) where T : class
        => result.Value ?? Assert.IsType<T>(Assert.IsType<ObjectResult>(result.Result, exactMatch: false).Value, exactMatch: false);

    #endregion

    #region Texts | Query

    [Fact]
    public void AllTextsListTitlesThenOverviewsWithTheFiltersPassedOn()
    {
        var fixture = new Fixture();
        var options = new List<(TextKind Kind, TextFilteringOptions Options)>();
        fixture.Text.Setup(t => t.GetAllTexts(It.IsAny<TextKind>(), It.IsAny<TextFilteringOptions?>()))
            .Callback<TextKind, TextFilteringOptions?>((kind, filter) => options.Add((kind, filter!)))
            .Returns<TextKind, TextFilteringOptions?>((kind, _) => kind is TextKind.Title ? [Title(1, "A"), Title(2, "B"), Title(3, "C")] : [Overview(1, "D")]);

        var result = Value(fixture.Controller().GetAllTexts(entitySource: MetadataSource.AniDB, entityType: MetadataEntityType.Series, source: MetadataSource.AniDB, language: [TitleLanguage.English], pageSize: 2, page: 2));

        Assert.Equal(4, result.Total);
        Assert.Equal(["C", "D"], result.List.Select(text => text.Value));
        Assert.Equal([TextKind.Title, TextKind.Overview], options.Select(pair => pair.Kind));
        var passed = options[0].Options;
        Assert.Equal((MetadataSource.AniDB, MetadataEntityType.Series, MetadataSource.AniDB, TitleLanguage.English), (passed.EntitySource!, passed.EntityType!, passed.Source!, passed.Language));
        Assert.Null(passed.IsEnabled);
        Assert.False(passed.IncludeInlineDefault);
    }

    [Fact]
    public void AllTextsKeepSeveralLanguagesAfterTheManagerAnswers()
    {
        var fixture = new Fixture();
        fixture.Text.Setup(t => t.GetAllTexts(TextKind.Title, It.Is<TextFilteringOptions?>(o => o!.Language == null)))
            .Returns([Title(1, "A"), Title(2, "B", TitleLanguage.Japanese, "ja"), Title(3, "C", TitleLanguage.German, "de")]);

        var result = Value(fixture.Controller().GetAllTexts(kind: TextKind.Title, language: [TitleLanguage.English, TitleLanguage.German]));

        Assert.Equal(["A", "C"], result.List.Select(text => text.Value));
    }

    [Fact]
    public void ATextIsFoundByItsKindAndID()
    {
        var fixture = new Fixture();
        fixture.Text.Setup(t => t.GetTitleByID(5)).Returns(Title(5, "Title"));
        fixture.Text.Setup(t => t.GetOverviewByID(5)).Returns(Overview(5, "Overview"));

        var title = Value(fixture.Controller().GetTextByID(TextKind.Title, 5));
        var overview = Value(fixture.Controller().GetTextByID(TextKind.Overview, 5));

        Assert.Equal((5, TextKind.Title, "Title", TitleType.Official), (title.ID!.Value, title.Kind, title.Value, title.TitleType!.Value));
        Assert.Equal((MetadataSource.AniDB, MetadataEntityType.Series, "1"), (title.EntitySource!, title.EntityType!, title.EntityID));
        Assert.Equal((TextKind.Overview, "Overview"), (overview.Kind, overview.Value));
        Assert.Null(overview.TitleType);
        Assert.IsType<NotFoundObjectResult>(fixture.Controller().GetTextByID(TextKind.Title, 6).Result);
    }

    [Fact]
    public void OrphanedTextsAreTheStoredTextsOfEntriesThatAreGone()
    {
        var fixture = new Fixture();
        var gone = new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, "404");
        fixture.Text.Setup(t => t.GetOrphanedEntries(MetadataSource.AniDB)).Returns([gone]);
        fixture.Text.Setup(t => t.GetTitles(gone, It.Is<TextFilteringOptions?>(o => o!.IsEnabled == null && !o.IncludeInlineDefault))).Returns([Title(7, "Lost", entity: gone)]);
        fixture.Text.Setup(t => t.GetOverviews(gone, It.IsAny<TextFilteringOptions?>())).Returns([]);

        var result = Value(fixture.Controller().GetOrphanedTexts(MetadataSource.AniDB));

        Assert.Equal(1, result.Total);
        Assert.Equal(("Lost", "404"), (result.List[0].Value, result.List[0].EntityID));
    }

    #endregion

    #region Texts | Mutations

    [Fact]
    public void AnUpdateGoesToTheManagerAndARefusedValueIsABadRequest()
    {
        var fixture = new Fixture();
        var text = Title(5, "Old", source: MetadataSource.User);
        fixture.Text.Setup(t => t.GetTitleByID(5)).Returns(text);
        fixture.Text.Setup(t => t.UpdateText(text, It.Is<TextUpdateData>(d => d.Value == "New" && d.Ordering == 2 && d.IsEnabled == null)))
            .Returns(Title(5, "New", source: MetadataSource.User));
        fixture.Text.Setup(t => t.UpdateText(text, It.Is<TextUpdateData>(d => d.Value == "Refused")))
            .Throws(new InvalidOperationException("Only a text a user added themselves can be given a new value."));

        var updated = Value(fixture.Controller().UpdateText(TextKind.Title, 5, new() { Value = "New", Ordering = 2 }));
        var refused = fixture.Controller().UpdateText(TextKind.Title, 5, new() { Value = "Refused" });
        var missing = fixture.Controller().UpdateText(TextKind.Title, 6, new() { Value = "New" });

        Assert.Equal("New", updated.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ObjectResult>(refused.Result, exactMatch: false).StatusCode);
        Assert.IsType<NotFoundObjectResult>(missing.Result);
    }

    [Fact]
    public void ABatchUpdateChangesEachTextAndOnlyFailsWhenNoneCouldBe()
    {
        var fixture = new Fixture();
        var title = Title(5, "Title");
        var overview = Overview(5, "Overview");
        fixture.Text.Setup(t => t.GetTitleByID(5)).Returns(title);
        fixture.Text.Setup(t => t.GetOverviewByID(5)).Returns(overview);
        fixture.Text.Setup(t => t.UpdateText(title, It.Is<TextUpdateData>(d => d.IsEnabled == false && d.Ordering == null))).Returns(title);
        fixture.Text.Setup(t => t.UpdateText(overview, It.Is<TextUpdateData>(d => d.Ordering == 3 && d.IsEnabled == null))).Returns(overview);

        var result = Value(fixture.Controller().BatchUpdateTexts(new()
        {
            Texts =
            [
                new() { Kind = TextKind.Title, ID = 5, IsEnabled = false },
                new() { Kind = TextKind.Overview, ID = 5, Ordering = 3 },
                new() { Kind = TextKind.Title, ID = 6, IsEnabled = false },
            ],
        }));
        var none = fixture.Controller().BatchUpdateTexts(new() { Texts = [new() { Kind = TextKind.Title, ID = 6 }, new() { ID = 5 }] });

        Assert.Equal(["Title", "Overview"], result.List.Select(text => text.Value));
        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ObjectResult>(none.Result, exactMatch: false).StatusCode);
        fixture.Text.Verify(t => t.UpdateText(It.IsAny<IText>(), It.IsAny<TextUpdateData>()), Times.Exactly(2));
    }

    [Fact]
    public void AStoredTextIsPreferredOnItsOwnEntry()
    {
        var fixture = new Fixture();
        var text = Title(5, "Title");
        fixture.Text.Setup(t => t.GetTitleByID(5)).Returns(text);
        fixture.Text.Setup(t => t.SetPreferredTitle(_anime, text, true)).Returns(new StoredTitle { ID = 5, EntityID = _anime, Source = MetadataSource.AniDB, Value = "Title", Language = TitleLanguage.English, LanguageCode = "en", Preference = TextPreference.Language });

        Value(fixture.Controller().SetPreferredText(TextKind.Title, 5, languageOnly: true));

        fixture.Text.Verify(t => t.SetPreferredTitle(_anime, text, true), Times.Once);
    }

    [Fact]
    public void UnsettingAPreferenceTheTextDidNotCarryIsABadRequest()
    {
        var fixture = new Fixture();
        var carried = Title(5, "Picked");
        var plain = Title(6, "Plain");
        fixture.Text.Setup(t => t.GetTitleByID(5)).Returns(carried);
        fixture.Text.Setup(t => t.GetTitleByID(6)).Returns(plain);
        fixture.Text.Setup(t => t.UnsetPreferredText(carried)).Returns(true);

        Assert.IsType<NoContentResult>(fixture.Controller().UnsetPreferredText(TextKind.Title, 5));
        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ObjectResult>(fixture.Controller().UnsetPreferredText(TextKind.Title, 6), exactMatch: false).StatusCode);
        Assert.IsType<NotFoundObjectResult>(fixture.Controller().UnsetPreferredText(TextKind.Title, 7));
    }

    [Fact]
    public void ATextIsRemoved()
    {
        var fixture = new Fixture();
        var text = Overview(5, "Overview");
        fixture.Text.Setup(t => t.GetOverviewByID(5)).Returns(text);
        fixture.Text.Setup(t => t.RemoveText(text)).Returns(true);

        Assert.IsType<NoContentResult>(fixture.Controller().RemoveText(TextKind.Overview, 5));
        Assert.IsType<NotFoundObjectResult>(fixture.Controller().RemoveText(TextKind.Title, 5));
        fixture.Text.Verify(t => t.RemoveText(text), Times.Once);
    }

    #endregion

    #region Entities

    [Fact]
    public void AnEntrysTextsStartWithTheInlineDefaultAndMarkTheChosenOne()
    {
        var fixture = new Fixture();
        var show = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Series, "7");
        fixture.Stores(show);
        var inline = new StoredTitle { Source = MetadataSource.TMDB, Value = "Show", Language = TitleLanguage.EnglishAmerican, LanguageCode = "en", CountryCode = "US", IsInlineDefault = true };
        var translation = Title(3, "Série", TitleLanguage.French, "fr", MetadataSource.TMDB, entity: show);
        fixture.Text.Setup(t => t.GetTitles(show, It.Is<TextFilteringOptions?>(o => o!.IncludeInlineDefault && o.IsEnabled == null))).Returns([inline, translation]);
        fixture.Text.Setup(t => t.GetDefaultTitle(show)).Returns(inline);
        fixture.Text.Setup(t => t.GetPreferredTitle(show)).Returns(translation);

        var result = Value(fixture.Controller().GetTextsForEntity(MetadataSource.TMDB, MetadataEntityType.Series, "7", kind: TextKind.Title));

        Assert.Equal(2, result.Total);
        var (first, second) = (result.List[0], result.List[1]);
        Assert.Equal(("Show", true, (int?)null, true, false), (first.Value, first.Inline, first.ID, first.Default!.Value, first.Preferred!.Value));
        Assert.Equal(("Série", false, 3, false, true), (second.Value, second.Inline, second.ID!.Value, second.Default!.Value, second.Preferred!.Value));

        var json = JObject.Parse(JsonConvert.SerializeObject(first, _apiSettings));
        Assert.Equal(JTokenType.Null, json["ID"]!.Type);
        Assert.Equal("en-US", json["Language"]!.Value<string>());
    }

    [Fact]
    public void AnEntrysDefaultIsNotListedTwiceNorWhenFilteredOut()
    {
        var fixture = new Fixture();
        fixture.Stores(_anime);
        var inline = new StoredOverview { Source = MetadataSource.AniDB, Value = "Described.", Language = TitleLanguage.English, LanguageCode = "en", IsInlineDefault = true };
        fixture.Text.Setup(t => t.GetOverviews(_anime, It.IsAny<TextFilteringOptions?>()))
            .Returns<MetadataGuid, TextFilteringOptions?>((_, options) => options!.Source is null ? [inline] : []);
        fixture.Text.Setup(t => t.GetDefaultOverview(_anime)).Returns(inline);

        var listed = Value(fixture.Controller().GetTextsForEntity(MetadataSource.AniDB, MetadataEntityType.Series, "1", kind: TextKind.Overview));
        var filtered = Value(fixture.Controller().GetTextsForEntity(MetadataSource.AniDB, MetadataEntityType.Series, "1", kind: TextKind.Overview, source: MetadataSource.TMDB));

        Assert.Equal(1, listed.Total);
        Assert.Equal(0, filtered.Total);
    }

    [Fact]
    public void AnEntryThatCannotBeFoundIsNotFound()
    {
        var fixture = new Fixture();

        Assert.IsType<NotFoundObjectResult>(fixture.Controller().GetTextsForEntity(MetadataSource.AniDB, MetadataEntityType.Series, "404").Result);
        Assert.IsType<NotFoundObjectResult>(fixture.Controller().AddText(MetadataSource.AniDB, MetadataEntityType.Series, "404", new() { Kind = TextKind.Title, Value = "X" }).Result);
        Assert.IsType<NotFoundObjectResult>(fixture.Controller().GetPreferredTextForEntity(MetadataSource.AniDB, MetadataEntityType.Series, "404").Result);
        Assert.IsType<NotFoundObjectResult>(fixture.Controller().SetPreferredTextForEntity(MetadataSource.AniDB, MetadataEntityType.Series, "404", new() { Kind = TextKind.Title, Default = true }).Result);
        Assert.IsType<NotFoundObjectResult>(fixture.Controller().GetTextsForEntity(MetadataSource.AniDB, MetadataEntityType.Series, " padded ").Result);
    }

    [Fact]
    public void AUsersTextIsAddedToAnEntry()
    {
        var fixture = new Fixture();
        fixture.Stores(_anime);
        TextData? passed = null;
        fixture.Text.Setup(t => t.AddText(_anime, It.IsAny<TextData>()))
            .Callback<MetadataGuid, TextData>((_, data) => passed = data)
            .Returns(Title(9, "Mine", source: MetadataSource.User, type: TitleType.Synonym));

        var result = Assert.IsType<CreatedResult>(fixture.Controller().AddText(MetadataSource.AniDB, MetadataEntityType.Series, "1", new()
        {
            Kind = TextKind.Title,
            Value = "Mine",
            LanguageCode = "en",
            TitleType = TitleType.Synonym,
            Preference = TextPreference.Language,
        }).Result);

        Assert.Equal("/api/v3/Text/Management/Title/9", result.Location);
        Assert.Equal((MetadataSource.User, TitleType.Synonym, TextPreference.Language, true), (passed!.Source!, passed.TitleType, passed.Preference, passed.IsEnabled));
    }

    [Fact]
    public void ARefusedTextIsABadRequest()
    {
        var fixture = new Fixture();
        fixture.Stores(_anime);
        fixture.Text.Setup(t => t.AddText(_anime, It.IsAny<TextData>())).Throws(new ArgumentException("A text must have a value."));

        var result = fixture.Controller().AddText(MetadataSource.AniDB, MetadataEntityType.Series, "1", new() { Kind = TextKind.Title, Value = " " });

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ObjectResult>(result.Result, exactMatch: false).StatusCode);
    }

    [Fact]
    public void AnEntrysTextsAreRemovedBySourceEvenWhenTheEntryIsGone()
    {
        var fixture = new Fixture();
        var gone = new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, "404");
        fixture.Text.Setup(t => t.RemoveTexts(gone, MetadataSource.User)).Returns(2);

        var result = Assert.IsType<OkObjectResult>(fixture.Controller().RemoveTextsForEntity(MetadataSource.AniDB, MetadataEntityType.Series, "404", MetadataSource.User).Result);

        Assert.Equal(2, result.Value);
    }

    [Fact]
    public void ThePreferredRouteReportsTheChosenTextWithItsStep()
    {
        var fixture = new Fixture();
        fixture.Stores(_anime);
        var main = Title(1, "Main", TitleLanguage.Romaji, "x-jat", type: TitleType.Main);
        fixture.Text.Setup(t => t.GetPreferredTitle(_anime)).Returns(main);
        fixture.Text.Setup(t => t.GetDefaultTitle(_anime)).Returns(main);

        var result = Value(fixture.Controller().GetPreferredTextForEntity(MetadataSource.AniDB, MetadataEntityType.Series, "1"));

        Assert.Equal((TextKind.Title, TextChoiceStep.LanguageOrder, "x-main", MetadataSource.AniDB), (result.Kind, result.Step, result.Language, result.Source!));
        Assert.Equal(("Main", "Main"), (result.Text!.Value, result.Default!.Value));
    }

    [Fact]
    public void AStoredTextOfAnotherEntryIsPickedByID()
    {
        var fixture = new Fixture();
        var series = new MetadataGuid(MetadataSource.Shoko, MetadataEntityType.Series, "3");
        fixture.Stores(series);
        var anidbTitle = Title(4, "Official");
        fixture.Text.Setup(t => t.GetTitleByID(4)).Returns(anidbTitle);
        fixture.Text.Setup(t => t.SetPreferredTitle(series, anidbTitle, false)).Returns(new StoredTitle { ID = 11, EntityID = series, ReferenceID = 4, Source = MetadataSource.User, Value = "Official", Language = TitleLanguage.English, LanguageCode = "en", Preference = TextPreference.Overall });

        var result = Value(fixture.Controller().SetPreferredTextForEntity(MetadataSource.Shoko, MetadataEntityType.Series, "3", new() { Kind = TextKind.Title, TextID = 4 }));

        Assert.Equal((11, 4, TextPreference.Overall), (result.ID!.Value, result.ReferenceID!.Value, result.Preference));
        Assert.IsType<NotFoundObjectResult>(fixture.Controller().SetPreferredTextForEntity(MetadataSource.Shoko, MetadataEntityType.Series, "3", new() { Kind = TextKind.Title, TextID = 5 }).Result);
    }

    [Fact]
    public void TheDefaultOrANewValueIsPicked()
    {
        var fixture = new Fixture();
        fixture.Stores(_anime);
        var inline = new StoredOverview { Source = MetadataSource.AniDB, Value = "Described.", Language = TitleLanguage.English, LanguageCode = "en", IsInlineDefault = true };
        fixture.Text.Setup(t => t.GetDefaultOverview(_anime)).Returns(inline);
        fixture.Text.Setup(t => t.SetPreferredOverview(_anime, inline, false)).Returns(Overview(20, "Described.", MetadataSource.User));
        IText? typed = null;
        fixture.Text.Setup(t => t.SetPreferredTitle(_anime, It.Is<ITitle>(title => title.Value == "Typed"), true))
            .Callback<MetadataGuid, ITitle, bool>((_, title, _) => typed = title)
            .Returns(Title(21, "Typed", TitleLanguage.German, "de", MetadataSource.User));

        var byDefault = Value(fixture.Controller().SetPreferredTextForEntity(MetadataSource.AniDB, MetadataEntityType.Series, "1", new() { Kind = TextKind.Overview, Default = true }));
        var byValue = Value(fixture.Controller().SetPreferredTextForEntity(MetadataSource.AniDB, MetadataEntityType.Series, "1", new() { Kind = TextKind.Title, Value = "Typed", LanguageCode = "de", LanguageOnly = true }));

        Assert.Equal(20, byDefault.ID);
        Assert.Equal(21, byValue.ID);
        Assert.Equal((MetadataSource.User, TitleLanguage.German, "de"), (typed!.Source, typed.Language, typed.LanguageCode));
    }

    [Theory]
    [InlineData(false, false, null)]
    [InlineData(true, true, null)]
    [InlineData(false, true, "Typed")]
    public void APickMustNameExactlyOneText(bool byID, bool byDefault, string? value)
    {
        var fixture = new Fixture();
        fixture.Stores(_anime);

        var result = fixture.Controller().SetPreferredTextForEntity(MetadataSource.AniDB, MetadataEntityType.Series, "1", new()
        {
            Kind = TextKind.Title,
            TextID = byID ? 4 : null,
            Default = byDefault,
            Value = value,
        });

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ObjectResult>(result.Result, exactMatch: false).StatusCode);
    }

    [Fact]
    public void ABodyThatNamesNoKindIsABadRequest()
    {
        var fixture = new Fixture();
        fixture.Stores(_anime);
        var pick = JsonConvert.DeserializeObject<SetPreferredTextBody>("{\"TextID\": 12}", new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate })!;
        var added = JsonConvert.DeserializeObject<AddTextBody>("{\"Value\": \"Typed\"}", new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate })!;

        var picked = fixture.Controller().SetPreferredTextForEntity(MetadataSource.AniDB, MetadataEntityType.Series, "1", pick);
        var stored = fixture.Controller().AddText(MetadataSource.AniDB, MetadataEntityType.Series, "1", added);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ObjectResult>(picked.Result, exactMatch: false).StatusCode);
        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ObjectResult>(stored.Result, exactMatch: false).StatusCode);
        fixture.Text.Verify(t => t.GetTitleByID(It.IsAny<int>()), Times.Never);
        fixture.Text.Verify(t => t.AddText(It.IsAny<MetadataGuid>(), It.IsAny<TextData>()), Times.Never);
    }

    [Fact]
    public void PickingTheDefaultOfAnEntryWithoutOneIsABadRequest()
    {
        var fixture = new Fixture();
        fixture.Stores(_anime);

        var result = fixture.Controller().SetPreferredTextForEntity(MetadataSource.AniDB, MetadataEntityType.Series, "1", new() { Kind = TextKind.Overview, Default = true });

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ObjectResult>(result.Result, exactMatch: false).StatusCode);
    }

    #endregion

    #region Chooser Steps

    private static (TextChoiceStep Step, TitleLanguage? Language) StepOf(MetadataGuid entity, TextKind kind, IText? chosen, IText? defaultText, Action<ServerSettings>? configure = null)
    {
        var settings = new ServerSettings();
        configure?.Invoke(settings);
        return TextChoiceExplainer.StepOf(entity, kind, chosen, defaultText, settings);
    }

    [Fact]
    public void NoTextIsNoStep()
        => Assert.Equal((TextChoiceStep.None, (TitleLanguage?)null), StepOf(_anime, TextKind.Title, null, null));

    [Fact]
    public void AUsersPicksAreTheirOwnSteps()
    {
        var overall = new StoredTitle { ID = 1, EntityID = _anime, Source = MetadataSource.User, Value = "Mine", Language = TitleLanguage.Unknown, LanguageCode = "unk", Preference = TextPreference.Overall };
        var forLanguage = new StoredTitle { ID = 2, EntityID = _anime, Source = MetadataSource.AniDB, Value = "Mine", Language = TitleLanguage.English, LanguageCode = "en", Preference = TextPreference.Language };

        Assert.Equal(TextChoiceStep.OverallPreference, StepOf(_anime, TextKind.Title, overall, null).Step);
        Assert.Equal((TextChoiceStep.LanguagePreference, (TitleLanguage?)TitleLanguage.English), StepOf(_anime, TextKind.Title, forLanguage, null, s => s.Language.SeriesTitleLanguageOrder = ["x-main", "en"]));
    }

    [Fact]
    public void ATextFoundInAConfiguredLanguageNamesThatLanguage()
    {
        var english = Title(2, "Official");

        Assert.Equal((TextChoiceStep.LanguageOrder, (TitleLanguage?)TitleLanguage.English), StepOf(_anime, TextKind.Title, english, null, s => s.Language.SeriesTitleLanguageOrder = ["x-main", "en"]));
        Assert.Equal((TextChoiceStep.Default, (TitleLanguage?)null), StepOf(_anime, TextKind.Title, english, english, s => s.Language.SeriesTitleLanguageOrder = ["ja"]));
    }

    [Fact]
    public void ATextFromASourceNoOneRankedIsTheDefault()
    {
        var series = new MetadataGuid(MetadataSource.Shoko, MetadataEntityType.Series, "3");
        var tmdb = Title(2, "Official", source: MetadataSource.TMDB);

        Assert.Equal(TextChoiceStep.LanguageOrder, StepOf(series, TextKind.Title, tmdb, null, s => s.Language.SeriesTitleLanguageOrder = ["en"]).Step);
        Assert.Equal(TextChoiceStep.Default, StepOf(series, TextKind.Title, tmdb, null, s =>
        {
            s.Language.SeriesTitleLanguageOrder = ["en"];
            s.Language.SeriesTitleSourceOrder = [MetadataSource.AniDB];
        }).Step);
    }

    [Fact]
    public void AnEpisodesGenericAndMadeUpTitlesHaveTheirOwnSteps()
    {
        var generic = Title(2, "Episode 5", source: MetadataSource.User, entity: _episode);
        var anidbGeneric = Title(3, "Episode 5", entity: _episode);
        var synthesized = new StoredTitle { Source = MetadataSource.Shoko, Value = "Episode 5", Language = TitleLanguage.English, LanguageCode = "en", IsSynthesized = true };

        Assert.Equal((TextChoiceStep.GenericTitle, (TitleLanguage?)TitleLanguage.English), StepOf(_episode, TextKind.Title, generic, null));
        Assert.Equal((TextChoiceStep.GenericTitle, (TitleLanguage?)TitleLanguage.English), StepOf(_episode, TextKind.Title, anidbGeneric, null));
        Assert.Equal(TextChoiceStep.Synthesized, StepOf(_episode, TextKind.Title, synthesized, null).Step);
    }

    [Fact]
    public void OverviewsWalkTheOverviewOrders()
    {
        var overview = Overview(2, "Described.", MetadataSource.TMDB);

        Assert.Equal((TextChoiceStep.LanguageOrder, (TitleLanguage?)TitleLanguage.English), StepOf(_anime, TextKind.Overview, overview, null));
        Assert.Equal(TextChoiceStep.Default, StepOf(_anime, TextKind.Overview, overview, overview, s => s.Language.DescriptionLanguageOrder = ["de"]).Step);
    }

    #endregion
}
