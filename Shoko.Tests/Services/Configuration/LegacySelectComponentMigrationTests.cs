using Newtonsoft.Json;
using Shoko.Abstractions.UI.Components;
using Shoko.Server.Services.Configuration;
using Xunit;

namespace Shoko.Tests.Services.Configuration;

public class LegacySelectComponentMigrationTests
{
    private const string Legacy = """
        {
          "SonarrQualityProfile": {
            "options": [
              { "label": "HD", "groupId": 1, "value": 4, "selected": true, "default": false, "disabled": false }
            ],
            "groups": [
              { "id": 1, "label": "Profiles", "disabled": false }
            ]
          },
          "Other": { "options": "not a select" }
        }
        """;

    [Fact]
    public void RewritesOldKeysSoTheValueDeserializes()
    {
        var migrated = LegacySelectComponentMigration.Apply(Legacy);

        var select = JsonConvert.DeserializeObject<Wrapper>(migrated)!.SonarrQualityProfile;
        var option = Assert.Single(select.Options);
        Assert.Equal(4, option.Value);
        Assert.Equal("HD", option.Label);
        Assert.Equal(1u, option.GroupID);
        Assert.True(option.IsSelected);
        var group = Assert.Single(select.Groups);
        Assert.Equal(1u, group.ID);
        Assert.Equal("Profiles", group.Label);
        Assert.DoesNotContain("\"groupId\"", migrated);
        Assert.Contains("\"Other\": {", migrated);
        Assert.Contains("\"options\": \"not a select\"", migrated);
    }

    [Fact]
    public void LeavesCurrentDocumentsUntouched()
    {
        var current = LegacySelectComponentMigration.Apply(Legacy);

        Assert.Same(current, LegacySelectComponentMigration.Apply(current));
    }

    private sealed class Wrapper
    {
        public SelectComponent<int> SonarrQualityProfile { get; set; } = new();
    }
}
