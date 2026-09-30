using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Tasks;
using Xunit;
using static Shoko.IntegrationTests.Sql;

namespace Shoko.IntegrationTests;

/// <summary>
/// Runs the step that copies the names and overviews users gave their series, episodes and groups
/// out of the released columns into the text store, and checks every copied value, what the
/// entries are called afterwards, and that renaming, resetting and deleting keep the store in step.
/// Also covers recreating every group and removing the groups a moved series left empty.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ShokoTextMigrationTests(DatabaseMigrationFixture fixture)
{
    #region Fixture Data

    private const int NamedAnimeID = 987_931;

    private const int NamedEpisodeID = 987_932;

    private const int AutoAnimeID = 987_933;

    private const int RegroupedAnimeID = 987_934;

    private const int MovedAnimeID = 987_935;

    #endregion

    #region Helpers

    private static string Row(MetadataEntityType type, int id, bool title, string value)
    {
        var shoko = MetadataNumberRegistry.GetNumber(MetadataSource.Shoko);
        var user = MetadataNumberRegistry.GetNumber(MetadataSource.User);
        var titleType = title ? $"{(int)TitleType.Main}|" : string.Empty;
        return $"{shoko}|{MetadataNumberRegistry.GetNumber(type)}|{id}|{user}|{TitleLanguage.Unknown.GetString()}|unk|NULL|NULL|{titleType}{value}|1|{(int)TextPreference.Overall}|0|NULL";
    }

    private static List<string> Stored(IDbConnection connection, string table, string ids)
    {
        var shoko = MetadataNumberRegistry.GetNumber(MetadataSource.Shoko);
        var titleType = table is "Metadata_Title" ? "TitleType, " : string.Empty;
        return Read(connection, $"SELECT EntitySource, EntityType, EntityID, Source, Language, LanguageCode, CountryCode, ScriptCode, {titleType}Value, IsEnabled, Preference, Ordering, ReferenceID FROM {table} WHERE EntitySource = {shoko} AND EntityID IN ({ids})")
            .OrderBy(row => row, StringComparer.Ordinal)
            .ToList();
    }

    #endregion

    #region Tests

    [Fact]
    public void TheCopyStepMovesEveryNameAndTheNamesStayInStepAfterwards()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var services = fixture.Services;
        var animeRepository = services.GetRequiredService<AniDB_AnimeRepository>();
        var anidbEpisodes = services.GetRequiredService<AniDB_EpisodeRepository>();
        var groups = services.GetRequiredService<AnimeGroupRepository>();
        var seriesRepository = services.GetRequiredService<AnimeSeriesRepository>();
        var episodes = services.GetRequiredService<AnimeEpisodeRepository>();
        var texts = services.GetRequiredService<TextCache>();
        var groupManager = services.GetRequiredService<IShokoGroupManager>();

        animeRepository.Save(new AniDB_Anime { AnimeID = NamedAnimeID, MainTitle = "Named Anime", AnimeType = AnimeType.TVSeries, Description = "The named one.", AllTags = string.Empty });
        animeRepository.Save(new AniDB_Anime { AnimeID = AutoAnimeID, MainTitle = "Auto Anime", AnimeType = AnimeType.TVSeries, Description = "The other one.", AllTags = string.Empty });
        anidbEpisodes.Save(new AniDB_Episode { EpisodeID = NamedEpisodeID, AnimeID = NamedAnimeID, EpisodeType = EpisodeType.Episode, EpisodeNumber = 1, Description = string.Empty });
        var namedGroup = new AnimeGroup { DateTimeCreated = DateTime.Now, DateTimeUpdated = DateTime.Now };
        var autoGroup = new AnimeGroup { DateTimeCreated = DateTime.Now, DateTimeUpdated = DateTime.Now };
        var blankGroup = new AnimeGroup { DateTimeCreated = DateTime.Now, DateTimeUpdated = DateTime.Now };
        groups.Save(namedGroup, false);
        groups.Save(autoGroup, false);
        groups.Save(blankGroup, false);
        var namedSeries = new AnimeSeries { AniDB_ID = NamedAnimeID, AnimeGroupID = namedGroup.AnimeGroupID, DateTimeCreated = DateTime.Now, DateTimeUpdated = DateTime.Now, UpdatedAt = DateTime.Now };
        var autoSeries = new AnimeSeries { AniDB_ID = AutoAnimeID, AnimeGroupID = autoGroup.AnimeGroupID, DateTimeCreated = DateTime.Now, DateTimeUpdated = DateTime.Now, UpdatedAt = DateTime.Now };
        seriesRepository.Save(namedSeries, updateGroups: false, alsoupdateepisodes: false);
        seriesRepository.Save(autoSeries, updateGroups: false, alsoupdateepisodes: false);
        var namedEpisode = new AnimeEpisode { AnimeSeriesID = namedSeries.AnimeSeriesID, AniDB_EpisodeID = NamedEpisodeID, DateTimeCreated = DateTime.Now, DateTimeUpdated = DateTime.Now };
        episodes.Save(namedEpisode);

        var seriesIDs = $"'{namedSeries.AnimeSeriesID}', '{autoSeries.AnimeSeriesID}', '{namedEpisode.AnimeEpisodeID}', '{namedGroup.AnimeGroupID}', '{autoGroup.AnimeGroupID}', '{blankGroup.AnimeGroupID}'";
        using var connection = fixture.OpenConnection();
        try
        {
            // What a database upgraded from a released version holds until the drop steps run.
            ReleasedTextSchema.Restore(connection, fixture.Backend);

            // What upstream stored: a series and an episode renamed, one group named and described
            // by hand, and one named after a series that was renamed since.
            Execute(connection, "UPDATE AnimeSeries SET SeriesNameOverride = @value WHERE AnimeSeriesID = @id", ("@value", "My Series"), ("@id", namedSeries.AnimeSeriesID));
            Execute(connection, "UPDATE AnimeSeries SET SeriesNameOverride = @value WHERE AnimeSeriesID = @id", ("@value", string.Empty), ("@id", autoSeries.AnimeSeriesID));
            Execute(connection, "UPDATE AnimeEpisode SET EpisodeNameOverride = @value WHERE AnimeEpisodeID = @id", ("@value", "My Episode"), ("@id", namedEpisode.AnimeEpisodeID));
            Execute(connection, "UPDATE AnimeGroup SET GroupName = @name, IsManuallyNamed = 1, Description = @overview, OverrideDescription = 1 WHERE AnimeGroupID = @id",
                ("@name", "My Group"), ("@overview", "My overview."), ("@id", namedGroup.AnimeGroupID));
            Execute(connection, "UPDATE AnimeGroup SET GroupName = @name, IsManuallyNamed = 0, Description = @overview, OverrideDescription = 0 WHERE AnimeGroupID = @id",
                ("@name", "Stale Name"), ("@overview", "Stale overview."), ("@id", autoGroup.AnimeGroupID));

            // A group whose overview was cleared on purpose, and whose blank name named nothing.
            Execute(connection, "UPDATE AnimeGroup SET GroupName = @name, IsManuallyNamed = 1, Description = @overview, OverrideDescription = 1 WHERE AnimeGroupID = @id",
                ("@name", string.Empty), ("@overview", string.Empty), ("@id", blankGroup.AnimeGroupID));

            // Twice, as a step interrupted once is run again from the start.
            for (var run = 0; run < 2; run++)
                Run(DatabaseFixes.MigrateShokoTexts, connection);

            // Every copied value, exactly: the user's own texts, preferred over every other.
            Assert.Equal(
                new[]
                {
                    Row(MetadataEntityType.Series, namedSeries.AnimeSeriesID, true, "My Series"),
                    Row(MetadataEntityType.Episode, namedEpisode.AnimeEpisodeID, true, "My Episode"),
                    Row(MetadataEntityType.Collection, namedGroup.AnimeGroupID, true, "My Group"),
                }.OrderBy(row => row, StringComparer.Ordinal),
                Stored(connection, "Metadata_Title", seriesIDs)
            );
            Assert.Equal(
                new[]
                {
                    Row(MetadataEntityType.Collection, namedGroup.AnimeGroupID, false, "My overview."),
                    Row(MetadataEntityType.Collection, blankGroup.AnimeGroupID, false, string.Empty),
                }.OrderBy(row => row, StringComparer.Ordinal),
                Stored(connection, "Metadata_Overview", seriesIDs)
            );

            // What the entries are called, as a restart would read it.
            texts.Populate(false, TestContext.Current.CancellationToken);

            Assert.Equal("My Series", namedSeries.Title);
            Assert.Equal("My Series", namedSeries.Titles[0].Value);
            Assert.Equal("My Episode", namedEpisode.Title);
            Assert.NotNull(namedEpisode.CustomTitle);
            Assert.Null(autoSeries.CustomTitle);
            Assert.Equal("My Group", namedGroup.GroupName);
            Assert.Equal("My overview.", namedGroup.Description);
            Assert.True(((IShokoGroup)namedGroup).HasCustomTitle);
            Assert.True(((IShokoGroup)namedGroup).HasCustomOverview);
            var namedDto = new Group(namedGroup);
            Assert.Equal(("My Group", "My overview.", true, true), (namedDto.Name, namedDto.Description, namedDto.HasCustomName, namedDto.HasCustomDescription));

            // A group named after its main series reads the series' name as it is now.
            Assert.Equal(autoSeries.Title, autoGroup.GroupName);
            Assert.Equal(autoSeries.PreferredOverview?.Value ?? string.Empty, autoGroup.Description);
            Assert.False(((IShokoGroup)autoGroup).HasCustomTitle);
            var autoDto = new Group(autoGroup);
            Assert.Equal((autoSeries.Title, false, false), (autoDto.Name, autoDto.HasCustomName, autoDto.HasCustomDescription));

            // The cleared overview still hides any other, as it did before.
            Assert.Equal(string.Empty, blankGroup.Description);
            Assert.True(((IShokoGroup)blankGroup).HasCustomOverview);
            Assert.False(((IShokoGroup)blankGroup).HasCustomTitle);

            // Resetting the group hands its name and overview back to its main series, which the
            // user still calls by their own name.
            groupManager.UpdateGroup(namedGroup, new() { Name = null, Overview = null });
            Assert.Equal("My Series", namedGroup.GroupName);
            Assert.Equal(namedSeries.PreferredOverview?.Value ?? string.Empty, namedGroup.Description);
            Assert.False(((IShokoGroup)namedGroup).HasCustomTitle);
            Assert.Equal([Row(MetadataEntityType.Collection, blankGroup.AnimeGroupID, false, string.Empty)], Stored(connection, "Metadata_Overview", seriesIDs));

            // Clearing an overview on purpose keeps it, blank, and only null resets it.
            groupManager.UpdateGroup(autoGroup, new() { Overview = string.Empty });
            Assert.Equal(string.Empty, autoGroup.Description);
            Assert.True(new Group(autoGroup).HasCustomDescription);
            groupManager.UpdateGroup(autoGroup, new() { Overview = null });
            Assert.False(((IShokoGroup)autoGroup).HasCustomOverview);

            // Renaming it again stores one title, changed in place.
            groupManager.UpdateGroup(namedGroup, new() { Name = "Renamed" });
            groupManager.UpdateGroup(namedGroup, new() { Name = "Renamed Again" });
            Assert.Equal("Renamed Again", namedGroup.GroupName);
            var collection = MetadataNumberRegistry.GetNumber(MetadataEntityType.Collection).ToString();
            Assert.Equal(
                [Row(MetadataEntityType.Collection, namedGroup.AnimeGroupID, true, "Renamed Again")],
                Stored(connection, "Metadata_Title", $"'{namedGroup.AnimeGroupID}'").Where(row => row.Split('|')[1] == collection)
            );

            // Deleting the entries takes the names users gave them along.
            episodes.Delete(namedEpisode);
            seriesRepository.Delete(namedSeries);
            seriesRepository.Delete(autoSeries);
            groups.Delete(namedGroup);
            groups.Delete(autoGroup);
            groups.Delete(blankGroup);
            Assert.Empty(Stored(connection, "Metadata_Title", seriesIDs));
            Assert.Empty(Stored(connection, "Metadata_Overview", seriesIDs));
        }
        finally
        {
            ReleasedTextSchema.Drop(fixture, connection);
            if (episodes.GetByID(namedEpisode.AnimeEpisodeID) is { } leftEpisode)
                episodes.Delete(leftEpisode);
            foreach (var series in (AnimeSeries[])[namedSeries, autoSeries])
                if (seriesRepository.GetByID(series.AnimeSeriesID) is { } leftSeries)
                    seriesRepository.Delete(leftSeries);
            foreach (var group in (AnimeGroup[])[namedGroup, autoGroup, blankGroup])
                if (groups.GetByID(group.AnimeGroupID) is { } leftGroup)
                    groups.Delete(leftGroup);
            anidbEpisodes.Delete(anidbEpisodes.GetByEpisodeID(NamedEpisodeID)!);
            animeRepository.Delete(animeRepository.GetByAnimeID(NamedAnimeID)!);
            animeRepository.Delete(animeRepository.GetByAnimeID(AutoAnimeID)!);
        }
    }

    [Fact]
    public async Task RecreatingAllGroupsFinishesAndTakesTheOldGroupsTextsAlong()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var services = fixture.Services;
        var animeRepository = services.GetRequiredService<AniDB_AnimeRepository>();
        var groups = services.GetRequiredService<AnimeGroupRepository>();
        var seriesRepository = services.GetRequiredService<AnimeSeriesRepository>();
        var groupManager = services.GetRequiredService<IShokoGroupManager>();
        var groupCreator = services.GetRequiredService<AnimeGroupCreator>();

        animeRepository.Save(new AniDB_Anime { AnimeID = RegroupedAnimeID, MainTitle = "Regrouped Anime", AnimeType = AnimeType.TVSeries, Description = "Grouped again.", AllTags = string.Empty });
        var oldGroup = new AnimeGroup { DateTimeCreated = DateTime.Now, DateTimeUpdated = DateTime.Now };
        groups.Save(oldGroup, false);
        var series = new AnimeSeries { AniDB_ID = RegroupedAnimeID, AnimeGroupID = oldGroup.AnimeGroupID, DateTimeCreated = DateTime.Now, DateTimeUpdated = DateTime.Now, UpdatedAt = DateTime.Now };
        seriesRepository.Save(series, updateGroups: false, alsoupdateepisodes: false);

        using var connection = fixture.OpenConnection();
        var oldID = $"'{oldGroup.AnimeGroupID}'";
        try
        {
            groupManager.UpdateGroup(oldGroup, new() { Name = "My Group", Overview = "My overview." });
            Assert.Single(Stored(connection, "Metadata_Title", oldID));
            Assert.Single(Stored(connection, "Metadata_Overview", oldID));

            // The texts are removed while the groups are made again, which must not wait on the
            // groups' own transaction.
            await groupCreator.RecreateAllGroups();

            Assert.Null(groups.GetByID(oldGroup.AnimeGroupID));
            Assert.Empty(Stored(connection, "Metadata_Title", oldID));
            Assert.Empty(Stored(connection, "Metadata_Overview", oldID));
            var newGroup = seriesRepository.GetByID(series.AnimeSeriesID)!.AnimeGroup!;
            Assert.NotEqual(oldGroup.AnimeGroupID, newGroup.AnimeGroupID);
            Assert.False(((IShokoGroup)newGroup).HasCustomTitle);
            Assert.Equal(seriesRepository.GetByID(series.AnimeSeriesID)!.Title, newGroup.GroupName);
        }
        finally
        {
            if (seriesRepository.GetByID(series.AnimeSeriesID) is { } leftSeries)
            {
                var leftGroupID = leftSeries.AnimeGroupID;
                seriesRepository.Delete(leftSeries);
                if (groups.GetByID(leftGroupID) is { } leftGroup && leftGroup.AllSeries.Count == 0)
                    groups.Delete(leftGroup);
            }

            if (groups.GetByID(oldGroup.AnimeGroupID) is { } stillOld)
                groups.Delete(stillOld);
            animeRepository.Delete(animeRepository.GetByAnimeID(RegroupedAnimeID)!);
        }
    }

    [Fact]
    public void MovingTheLastSeriesOutRemovesEveryGroupItLeftEmpty()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var services = fixture.Services;
        var animeRepository = services.GetRequiredService<AniDB_AnimeRepository>();
        var groups = services.GetRequiredService<AnimeGroupRepository>();
        var seriesRepository = services.GetRequiredService<AnimeSeriesRepository>();
        var groupManager = services.GetRequiredService<IShokoGroupManager>();

        animeRepository.Save(new AniDB_Anime { AnimeID = MovedAnimeID, MainTitle = "Moved Anime", AnimeType = AnimeType.TVSeries, Description = "Moved out.", AllTags = string.Empty });
        var parent = new AnimeGroup { DateTimeCreated = DateTime.Now, DateTimeUpdated = DateTime.Now };
        groups.Save(parent, false);
        var child = new AnimeGroup { AnimeGroupParentID = parent.AnimeGroupID, DateTimeCreated = DateTime.Now, DateTimeUpdated = DateTime.Now };
        groups.Save(child, false);
        var series = new AnimeSeries { AniDB_ID = MovedAnimeID, AnimeGroupID = child.AnimeGroupID, DateTimeCreated = DateTime.Now, DateTimeUpdated = DateTime.Now, UpdatedAt = DateTime.Now };
        seriesRepository.Save(series, updateGroups: false, alsoupdateepisodes: false);

        try
        {
            Assert.Equal(series.Title, parent.GroupName);

            var moved = (AnimeGroup)groupManager.CreateGroup(new() { Series = [series] });

            // Neither group has a series left to be named after.
            Assert.Null(groups.GetByID(child.AnimeGroupID));
            Assert.Null(groups.GetByID(parent.AnimeGroupID));
            Assert.Equal(moved.AnimeGroupID, seriesRepository.GetByID(series.AnimeSeriesID)!.AnimeGroupID);
            Assert.Equal(series.Title, moved.GroupName);
        }
        finally
        {
            if (seriesRepository.GetByID(series.AnimeSeriesID) is { } leftSeries)
            {
                var leftGroupID = leftSeries.AnimeGroupID;
                seriesRepository.Delete(leftSeries);
                if (groups.GetByID(leftGroupID) is { } leftGroup && leftGroup.AllSeries.Count == 0)
                    groups.Delete(leftGroup);
            }

            foreach (var left in new[] { child.AnimeGroupID, parent.AnimeGroupID })
            {
                if (groups.GetByID(left) is { } leftGroup)
                    groups.Delete(leftGroup);
            }

            animeRepository.Delete(animeRepository.GetByAnimeID(MovedAnimeID)!);
        }
    }

    #endregion
}
