using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Server;
using Shoko.Tests.Infrastructure;
using Xunit;

using CreatorType = Shoko.Server.Providers.AniDB.CreatorType;

namespace Shoko.Tests.Models;

/// <summary>
/// Covers the works of an AniDB studio: the anime its company has a studio
/// role on, as the anime list their studios.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class AnidbStudioWorksTests
{
    [Fact]
    public void AStudiosWorksAreTheAnimeItHasAStudioRoleOn()
    {
        var tables = new AnidbPeopleTables();
        using var scope = tables.Scope();
        tables.Anime.Cache.Update(new AniDB_Anime { AniDB_AnimeID = 1, AnimeID = 30 });
        tables.Anime.Cache.Update(new AniDB_Anime { AniDB_AnimeID = 2, AnimeID = 31 });
        tables.Anime.Cache.Update(new AniDB_Anime { AniDB_AnimeID = 3, AnimeID = 32 });
        var company = new AniDB_Creator { AniDB_CreatorID = 1, CreatorID = 7, Name = "Sunrise", Type = CreatorType.Company };
        tables.Creators.Cache.Update(company);
        tables.StaffRows.AddRange([
            new() { AnimeID = 30, CreatorID = 7, Role = "Animation Work", RoleType = CreatorRoleType.Studio },
            new() { AnimeID = 30, CreatorID = 7, Role = "Work", RoleType = CreatorRoleType.Studio },
            new() { AnimeID = 31, CreatorID = 7, Role = "Animation Work", RoleType = CreatorRoleType.Studio },
            new() { AnimeID = 32, CreatorID = 7, Role = "Producer", RoleType = CreatorRoleType.Producer },
        ]);

        var studio = (IStudio)new AniDB_Studio(company);

        Assert.Equal(["30", "31"], studio.SeriesWorks.Select(series => series.ID.ID).Order());
        Assert.Equal(["30", "31"], studio.Works.Select(work => work.ID.ID).Order());
        Assert.Empty(studio.MovieWorks);
    }
}
