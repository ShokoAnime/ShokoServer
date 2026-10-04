using System;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.API.v3.Controllers;
using Shoko.Server.API.v3.Models.Ordering;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Services;
using Shoko.Server.Utilities;
using Xunit;
using static Shoko.IntegrationTests.Sql;

namespace Shoko.IntegrationTests;

/// <summary>
/// Checks that an ordering and its groups keep their names and descriptions in the text store: the
/// steps moving them out of the ordering tables, and a user's ordering made through APIv3.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class OrderingTextRoundTripTests(DatabaseMigrationFixture fixture)
{
    private const int AnimeID = 988_201;

    private const string OrderingID = "ordering-text-1";

    private static readonly DateTime _created = new(2024, 1, 1, 10, 0, 0);

    private static int Number(MetadataSource source)
        => MetadataNumberRegistry.GetNumber(source);

    private static int Kind(MetadataEntityType entityType)
        => MetadataNumberRegistry.GetNumber(entityType);

    /// <summary>
    /// Reads the ordering tables and the texts again from the database.
    /// </summary>
    private void Reload()
    {
        var services = fixture.Services;
        services.GetRequiredService<Metadata_OrderingRepository>().Populate(displayName: false);
        services.GetRequiredService<Metadata_Ordering_GroupRepository>().Populate(displayName: false);
        services.GetRequiredService<Metadata_Ordering_EntryRepository>().Populate(displayName: false);
        services.GetRequiredService<TextCache>().Populate(false, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void TheCopyStepMovesTheNamesIntoTheTextStoreAndTheDropStepsRemoveTheColumns()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);

        using var connection = fixture.OpenConnection();
        var plugin = Number(TestSources.Plugin);
        var groups = new[] { "ot-g1", "ot-g2", "ot-sp", "ot-g5", "ot-g6" };
        var entries = string.Join(", ", groups.Prepend(OrderingID).Select(id => $"'{id}'"));
        var stored = $"EntitySource = {plugin} AND Source = {plugin} AND EntityID IN ({entries})";
        try
        {
            ReleasedOrderingSchema.Restore(connection);
            Insert(
                connection,
                "Metadata_Ordering",
                ("Source", plugin),
                ("ProviderID", OrderingID),
                ("SeriesSource", plugin),
                ("SeriesID", "ot-series"),
                ("Type", (int)OrderingType.DVD),
                ("Name", "DVD Order"),
                ("Description", "As on the discs."),
                ("CreatedAt", _created),
                ("LastUpdatedAt", _created)
            );
            void Group(string id, int position, string name, bool special = false, int? seasonNumber = null, string? description = null)
                => Insert(
                    connection,
                    "Metadata_Ordering_Group",
                    ("Source", plugin),
                    ("ProviderID", id),
                    ("OrderingID", OrderingID),
                    ("Position", position),
                    ("IsSpecial", special),
                    ("SeasonNumber", seasonNumber),
                    ("Name", name),
                    ("Description", description)
                );

            // The generic names for each group's own number go, the others stay.
            Group("ot-g1", 0, "Season 1");
            Group("ot-g2", 1, "Season 1");
            Group("ot-sp", 2, "Specials", special: true);
            Group("ot-g5", 3, "Staffel 5", seasonNumber: 5);
            Group("ot-g6", 4, "Finale", description: "The end.");
            for (var run = 0; run < 2; run++)
                Run(DatabaseFixes.CopyOrderingTexts, connection);

            Assert.Equal(
                [
                    $"{Kind(MetadataEntityType.Ordering)}|{OrderingID}|unk|DVD Order",
                    $"{Kind(MetadataEntityType.Season)}|ot-g2|unk|Season 1",
                    $"{Kind(MetadataEntityType.Season)}|ot-g6|unk|Finale",
                ],
                Read(connection, $"SELECT EntityType, EntityID, LanguageCode, Value FROM Metadata_Title WHERE {stored} ORDER BY EntityID")
            );
            Assert.Equal(
                [$"{OrderingID}|As on the discs.", "ot-g6|The end."],
                Read(connection, $"SELECT EntityID, Value FROM Metadata_Overview WHERE {stored} ORDER BY EntityID")
            );

            foreach (var step in ReleasedOrderingSchema.DropSteps(fixture))
                Execute(connection, step.Command!);
            Assert.False(ReleasedOrderingSchema.AnyPresent(connection));
        }
        finally
        {
            ReleasedOrderingSchema.Drop(connection);
            Execute(connection, $"DELETE FROM Metadata_Title WHERE EntitySource = {plugin} AND EntityID IN ({entries})");
            Execute(connection, $"DELETE FROM Metadata_Overview WHERE EntitySource = {plugin} AND EntityID IN ({entries})");
            Execute(connection, $"DELETE FROM Metadata_Ordering_Group WHERE Source = {plugin} AND OrderingID = '{OrderingID}'");
            Execute(connection, $"DELETE FROM Metadata_Ordering WHERE Source = {plugin} AND ProviderID = '{OrderingID}'");
        }
    }

    [Fact]
    public async Task AUsersOrderingMadeThroughTheApiKeepsItsNamesAsUserTexts()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var services = fixture.Services;
        var orderings = services.GetRequiredService<IMetadataOrderingService>();

        // An anime in the collection with three episodes, and an admin to make the ordering.
        services.GetRequiredService<AniDB_AnimeRepository>().Save(new AniDB_Anime { AnimeID = AnimeID, MainTitle = "Ordered by hand" });
        foreach (var index in Enumerable.Range(0, 3))
            services.GetRequiredService<AniDB_EpisodeRepository>().Save(new AniDB_Episode
            {
                EpisodeID = AnimeID + 1 + index,
                AnimeID = AnimeID,
                EpisodeNumber = index + 1,
                EpisodeType = EpisodeType.Episode,
            });
        var group = new AnimeGroup { DateTimeCreated = DateTime.Now, DateTimeUpdated = DateTime.Now };
        services.GetRequiredService<AnimeGroupRepository>().Save(group, false);
        var series = new AnimeSeries
        {
            AniDB_ID = AnimeID,
            AnimeGroupID = group.AnimeGroupID,
            DateTimeCreated = DateTime.Now,
            DateTimeUpdated = DateTime.Now,
            UpdatedAt = DateTime.Now,
        };
        services.GetRequiredService<AnimeSeriesRepository>().Save(series, updateGroups: false, alsoupdateepisodes: false);
        var episodes = Enumerable.Range(0, 3).Select(index =>
        {
            var episode = new AnimeEpisode
            {
                AnimeSeriesID = series.AnimeSeriesID,
                AniDB_EpisodeID = AnimeID + 1 + index,
                DateTimeCreated = DateTime.Now,
                DateTimeUpdated = DateTime.Now,
            };
            services.GetRequiredService<AnimeEpisodeRepository>().Save(episode);
            return episode.AnimeEpisodeID;
        }).ToList();
        var admin = new JMMUser { Username = "ordering-text-admin", IsAdmin = 1, HideCategories = string.Empty, CanEditServerSettings = 1 };
        services.GetRequiredService<JMMUserRepository>().Save(admin);
        var controller = ActivatorUtilities.CreateInstance<SeriesOrderingController>(services);
        controller.ControllerContext = new()
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = services,
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, admin.JMMUserID.ToString())], "ShokoServer")),
            },
        };

        SeriesOrdering? made = null;
        try
        {
            var result = controller.CreateOrdering(series.AnimeSeriesID, new()
            {
                Name = "Hand Order",
                Description = "Made through the API.",
                Groups =
                [
                    new() { Name = "Season 1", EpisodeIDs = [episodes[0]] },
                    new() { Name = "Arc Two", EpisodeIDs = [episodes[1], episodes[2]] },
                ],
            });
            made = result.Value ?? Assert.IsType<SeriesOrdering>(Assert.IsType<ObjectResult>(result.Result, exactMatch: false).Value);
            Reload();

            // The ordering and its named group keep one user title each; the generic name is synthesized.
            var ordering = orderings.GetOrdering(MetadataGuid.Parse(made.ID, null))!;
            var texts = services.GetRequiredService<IMetadataTextManager>();
            Assert.Equal(("Hand Order", "Made through the API."), (ordering.Title, ordering.DefaultOverview?.Value));
            var stored = texts.GetTitles(ordering.ID).Single();
            Assert.Equal((MetadataSource.User, "unk", TitleType.Main), (stored.Source, stored.LanguageCode, stored.Type));
            Assert.Empty(services.GetRequiredService<TextCache>().GetRows(ordering.Seasons[0].ID));
            var languages = Languages.PreferredEpisodeNamingLanguages
                .Select(language => language.Language)
                .Where(language => language is not TitleLanguage.Main);
            var synthesized = GenericEpisodeTitles.SynthesizeSeasonAll(1, languages)[0].Value;
            Assert.Equal([synthesized, "Arc Two"], ordering.Seasons.Select(season => season.Title));
            Assert.Equal([synthesized, "Arc Two"], made.Groups!.Select(season => season.Name));
        }
        finally
        {
            if (made is not null)
                orderings.DeleteLocalOrdering(MetadataGuid.Parse(made.ID, null));
            services.GetRequiredService<JMMUserRepository>().Delete(admin);
            await services.GetRequiredService<IAnidbService>().PurgeAnimeByID(AnimeID, removeFromMylist: false);
        }
    }
}
