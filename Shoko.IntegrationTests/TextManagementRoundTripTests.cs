using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.API.v3.Controllers;
using Shoko.Server.API.v3.Models.TextManagement;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Drives the <c>Text/Management</c> routes against the started server's own
/// text manager, and reads what they wrote back after the caches are read
/// again from the database, on each backend.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class TextManagementRoundTripTests(DatabaseMigrationFixture fixture)
{
    #region Helpers

    private static readonly MetadataSource _source = TestSources.TextManagement;

    private static MetadataGuid ID(MetadataEntityType entityType, string id)
        => new(_source, entityType, id);

    private static TitleStub Title(string value, TitleLanguage language, string code, TitleType type)
        => new() { Source = _source, Value = value, Language = language, LanguageCode = code, Type = type };

    private TextManagementController Controller()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var services = fixture.Services;
        return new(services.GetRequiredService<IMetadataTextManager>(), services.GetRequiredService<IMetadataService>(), services.GetRequiredService<ISettingsProvider>())
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext { RequestServices = services } },
        };
    }

    /// <summary>
    /// Throws the caches away and reads the store tables and texts again from
    /// the database, forgetting every choice worked out before.
    /// </summary>
    private void Reload()
    {
        var services = fixture.Services;
        services.GetRequiredService<Metadata_SeriesRepository>().Populate(displayName: false);
        services.GetRequiredService<Metadata_CreatorRepository>().Populate(displayName: false);
        services.GetRequiredService<TextCache>().Populate(displayName: false);
        MetadataTextManager.OnLanguageSettingsChanged();
    }

    private static T Value<T>(ActionResult<T> result) where T : class
        => result.Value ?? Assert.IsType<T>(Assert.IsType<ObjectResult>(result.Result, exactMatch: false).Value, exactMatch: false);

    private static ManagedText Listed(TextManagementController controller, MetadataGuid id, string value)
        => Value(controller.GetTextsForEntity(id.Source, id.EntityType, id.ID, kind: TextKind.Title, pageSize: 0)).List.Single(text => text.Value == value);

    #endregion

    #region Tests

    [Fact]
    public void AUsersTextsAndPicksGoThroughTheRoutesAndLastInTheDatabase()
    {
        var controller = Controller();
        var seriesID = ID(MetadataEntityType.Series, "tm-s1");
        fixture.Services.GetRequiredService<IMetadataSeriesStore>().SaveSeries(new()
        {
            ID = seriesID,
            Titles =
            [
                Title("Main Title", TitleLanguage.Main, "x-main", TitleType.Main),
                Title("English Title", TitleLanguage.English, "en", TitleType.Official),
                Title("日本語", TitleLanguage.Japanese, "ja", TitleType.Official),
            ],
        });
        var (source, type, entity) = (seriesID.Source, seriesID.EntityType, seriesID.ID);

        // A user's own title, a source's title disabled, one preferred for
        // its language, and a new value picked over everything.
        var added = Value(controller.AddText(source, type, entity, new() { Kind = TextKind.Title, Value = "Mine", LanguageCode = "en", TitleType = TitleType.Synonym }));
        controller.EnableOrDisableText(TextKind.Title, Listed(controller, seriesID, "日本語").ID!.Value, new() { Enabled = false });
        controller.SetPreferredText(TextKind.Title, Listed(controller, seriesID, "English Title").ID!.Value, languageOnly: true);
        var picked = Value(controller.SetPreferredTextForEntity(source, type, entity, new() { Kind = TextKind.Title, Value = "Picked", LanguageCode = "en" }));

        Reload();

        var choice = Value(controller.GetPreferredTextForEntity(source, type, entity));
        Assert.Equal((TextChoiceStep.OverallPreference, "Picked", picked.ID), (choice.Step, choice.Text!.Value, choice.Text.ID));
        Assert.False(Listed(controller, seriesID, "日本語").IsEnabled);
        Assert.Equal(TextPreference.Language, Listed(controller, seriesID, "English Title").Preference);
        Assert.Equal("Mine", Value(controller.GetTextByID(TextKind.Title, added.ID!.Value)).Value);

        Assert.Equal(2, Assert.IsType<OkObjectResult>(controller.RemoveTextsForEntity(source, type, entity, MetadataSource.User).Result).Value);

        Reload();

        Assert.Equal(3, Value(controller.GetTextsForEntity(source, type, entity, kind: TextKind.Title)).Total);
        Assert.Empty(Value(controller.GetAllTexts(entitySource: _source, entityType: MetadataEntityType.Series, source: MetadataSource.User)).List);

        fixture.Services.GetRequiredService<IMetadataSeriesStore>().RemoveSeries(seriesID);
    }

    [Fact]
    public void ADefaultKeptOnTheRowIsPickedAsACopy()
    {
        var controller = Controller();
        var personID = ID(MetadataEntityType.Creator, "tm-p1");
        fixture.Services.GetRequiredService<IMetadataPeopleStore>().SaveCreators([new() { ID = personID, Name = "Kana" }]);
        var (source, type, entity) = (personID.Source, personID.EntityType, personID.ID);

        var picked = Value(controller.SetPreferredTextForEntity(source, type, entity, new() { Kind = TextKind.Title, Default = true }));
        Assert.Equal((MetadataSource.User, "Kana"), (picked.Source, picked.Value));

        Reload();

        var choice = Value(controller.GetPreferredTextForEntity(source, type, entity));
        Assert.Equal((TextChoiceStep.OverallPreference, picked.ID), (choice.Step, choice.Text!.ID));
        Assert.True(choice.Default!.Inline);
    }

    #endregion
}
