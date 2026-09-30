using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Writes titles and overviews through the text store into the migrated
/// database, then reads them back from it, so the two tables' mappings, their
/// column types and the text cache's own reads are checked on each backend.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class MetadataTextRoundTripTests(DatabaseMigrationFixture fixture)
{
    #region Helpers

    private static readonly MetadataSource _plugin = TestSources.Plugin;

    private static readonly MetadataGuid _entity = new(_plugin, MetadataEntityType.Series, "text-series-1");

    private static readonly MetadataGuid _other = new(_plugin, MetadataEntityType.Series, "text-series-2");

    private static readonly MetadataGuid _shokoEpisode = new(MetadataSource.Shoko, MetadataEntityType.Episode, "990001");

    private void Reload()
        => fixture.Services.GetRequiredService<TextCache>().Populate(displayName: false);

    #endregion

    #region Tests

    [Fact]
    public void WhatTheTextStoreWritesReadsBackFromTheDatabase()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var store = fixture.Services.GetRequiredService<MetadataTextStore>();
        var manager = fixture.Services.GetRequiredService<IMetadataTextManager>();
        var longText = new string('x', 6000);

        store.SetTitles(_entity, _plugin, [
            new TitleStub { Source = _plugin, Value = "Main", Language = TitleLanguage.Main, LanguageCode = "x-main", Type = TitleType.Main },
            new TitleStub { Source = _plugin, Value = "Canadian", Language = TitleLanguage.FrenchCanadian, LanguageCode = "fr", CountryCode = "CA", Type = TitleType.Synonym },
        ]);
        store.SetOverviews(_entity, _plugin, [new TextStub { Source = _plugin, Value = longText, Language = TitleLanguage.English, LanguageCode = "en" }]);
        store.SetTitles(_other, _plugin, [new TitleStub { Source = _plugin, Value = "Theirs", Language = TitleLanguage.English, LanguageCode = "en", Type = TitleType.Official }]);
        store.SetTitles(_shokoEpisode, _plugin, [new TitleStub { Source = _plugin, Value = "Contributed", Language = TitleLanguage.English, LanguageCode = "en" }]);
        manager.EnableText(store.GetTitles(_entity).Single(title => title.Value == "Canadian"), false);
        var pick = manager.SetPreferredTitle(_entity, Assert.Single(store.GetTitles(_other)), forLanguageOnly: true);

        Reload();

        var titles = store.GetTitles(_entity);
        Assert.Equal(["Main", "Canadian", "Theirs"], titles.Select(title => title.Value));
        Assert.Equal([TitleLanguage.Main, TitleLanguage.FrenchCanadian, TitleLanguage.English], titles.Select(title => title.Language));
        Assert.Equal([TitleType.Main, TitleType.Synonym, TitleType.Official], titles.Select(title => title.Type));
        Assert.Equal([null, "CA", null], titles.Select(title => title.CountryCode));
        Assert.Equal([_plugin, _plugin, MetadataSource.User], titles.Select(title => title.Source));
        Assert.Equal([true, false, true], titles.Select(title => title.IsEnabled));
        Assert.Equal([TextPreference.None, TextPreference.None, TextPreference.Language], titles.Select(title => title.Preference));
        Assert.Equal(pick.ID, titles[2].ID);
        Assert.Equal(store.GetTitles(_other).Single().ID, titles[2].ReferenceID);
        Assert.Equal(longText, Assert.Single(store.GetOverviews(_entity)).Value);
        Assert.Equal("Contributed", Assert.Single(store.GetTitles(_shokoEpisode)).Value);

        Assert.Equal([_shokoEpisode], store.RemoveSource(_plugin, entity => entity.Source == MetadataSource.Shoko));
        // The pick goes with the text it copies.
        Assert.Equal(1, store.RemoveEntry(_other));
        Assert.DoesNotContain(store.GetTitles(_entity), title => title.Source == MetadataSource.User);
        Assert.Equal(3, store.RemoveEntry(_entity));

        Reload();

        Assert.Empty(store.GetTitles(_entity));
        Assert.Empty(store.GetOverviews(_entity));
        Assert.Empty(store.GetTitles(_other));
        Assert.Empty(store.GetTitles(_shokoEpisode));
    }

    #endregion
}
