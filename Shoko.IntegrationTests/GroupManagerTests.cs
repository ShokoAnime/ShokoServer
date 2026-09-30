using System;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Checks what <see cref="IShokoGroupManager"/> refuses before it changes anything, and that a new
/// group is nested under the parent it was created with.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class GroupManagerTests(DatabaseMigrationFixture fixture)
{
    #region Fixture Data

    private const int FirstAnimeID = 987_941;

    private const int SecondAnimeID = 987_942;

    #endregion

    #region Helpers

    /// <summary>
    /// An implementation of a contract interface that is not the server's own, answering every
    /// member with its type's default.
    /// </summary>
    public class Foreign : DispatchProxy
    {
        /// <inheritdoc/>
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => targetMethod?.ReturnType is { IsValueType: true } type && type != typeof(void) ? Activator.CreateInstance(type) : null;
    }

    #endregion

    #region Tests

    [Fact]
    public void ForeignGroupsAndSeriesAndStrayMainSeriesAreRefusedAndAParentIsKept()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var services = fixture.Services;
        var animeRepository = services.GetRequiredService<AniDB_AnimeRepository>();
        var groups = services.GetRequiredService<AnimeGroupRepository>();
        var seriesRepository = services.GetRequiredService<AnimeSeriesRepository>();
        var groupManager = services.GetRequiredService<IShokoGroupManager>();

        animeRepository.Save(new AniDB_Anime { AnimeID = FirstAnimeID, MainTitle = "First Anime", AnimeType = AnimeType.TVSeries, Description = string.Empty, AllTags = string.Empty });
        animeRepository.Save(new AniDB_Anime { AnimeID = SecondAnimeID, MainTitle = "Second Anime", AnimeType = AnimeType.TVSeries, Description = string.Empty, AllTags = string.Empty });
        var parentGroup = new AnimeGroup { DateTimeCreated = DateTime.Now, DateTimeUpdated = DateTime.Now };
        var otherGroup = new AnimeGroup { DateTimeCreated = DateTime.Now, DateTimeUpdated = DateTime.Now };
        groups.Save(parentGroup, false);
        groups.Save(otherGroup, false);
        var first = new AnimeSeries { AniDB_ID = FirstAnimeID, AnimeGroupID = parentGroup.AnimeGroupID, DateTimeCreated = DateTime.Now, DateTimeUpdated = DateTime.Now, UpdatedAt = DateTime.Now };
        var second = new AnimeSeries { AniDB_ID = SecondAnimeID, AnimeGroupID = otherGroup.AnimeGroupID, DateTimeCreated = DateTime.Now, DateTimeUpdated = DateTime.Now, UpdatedAt = DateTime.Now };
        seriesRepository.Save(first, updateGroups: false, alsoupdateepisodes: false);
        seriesRepository.Save(second, updateGroups: false, alsoupdateepisodes: false);
        var foreignGroup = DispatchProxy.Create<IShokoGroup, Foreign>();
        var foreignSeries = DispatchProxy.Create<IShokoSeries, Foreign>();

        AnimeGroup? created = null;
        try
        {
            Assert.Throws<ArgumentException>(() => groupManager.UpdateGroup(foreignGroup, new() { Name = "Nope" }));
            Assert.Throws<ArgumentException>(() => groupManager.SetMainSeries(foreignGroup, first));
            Assert.Throws<ArgumentException>(() => groupManager.MoveSeries(foreignSeries, parentGroup));
            Assert.Throws<ArgumentException>(() => groupManager.MoveSeries(second, foreignGroup));

            var seriesRefused = Assert.Throws<GenericValidationException>(() => groupManager.UpdateGroup(parentGroup, new() { Series = [second, foreignSeries] }));
            Assert.Contains("Series", seriesRefused.ValidationErrors.Keys);
            Assert.Equal(otherGroup.AnimeGroupID, seriesRepository.GetByID(second.AnimeSeriesID)!.AnimeGroupID);

            var groupsRefused = Assert.Throws<GenericValidationException>(() => groupManager.UpdateGroup(parentGroup, new() { Groups = [foreignGroup] }));
            Assert.Contains("Groups", groupsRefused.ValidationErrors.Keys);

            // The new main series is checked, not the one the group had.
            var strayMain = Assert.Throws<GenericValidationException>(() => groupManager.SetMainSeries(parentGroup, second));
            Assert.Contains("PreferredSeries", strayMain.ValidationErrors.Keys);
            Assert.Null(groups.GetByID(parentGroup.AnimeGroupID)!.DefaultAnimeSeriesID);

            created = (AnimeGroup)groupManager.CreateGroup(new() { Series = [second], ParentGroup = parentGroup });
            Assert.Equal(parentGroup.AnimeGroupID, groups.GetByID(created.AnimeGroupID)!.AnimeGroupParentID);
        }
        finally
        {
            seriesRepository.Delete(seriesRepository.GetByID(first.AnimeSeriesID)!);
            seriesRepository.Delete(seriesRepository.GetByID(second.AnimeSeriesID)!);
            foreach (var groupID in new[] { created?.AnimeGroupID, otherGroup.AnimeGroupID, parentGroup.AnimeGroupID })
            {
                if (groupID is { } id && groups.GetByID(id) is { } left)
                    groups.Delete(left);
            }

            animeRepository.Delete(animeRepository.GetByAnimeID(FirstAnimeID)!);
            animeRepository.Delete(animeRepository.GetByAnimeID(SecondAnimeID)!);
        }
    }

    #endregion
}
