using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Server;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

using CreatorType = Shoko.Server.Providers.AniDB.CreatorType;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers the studios and networks <see cref="MetadataStudioStore"/> keeps and
/// the orphan stamps of both, against in-memory tables. A network
/// read through an entry looks itself up through <c>RepoFactory</c>, so these
/// tests share its collection.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class MetadataStudioStoreTests
{
    #region Helpers

    private static readonly MetadataGuid _series = new(TestSources.Plugin, MetadataEntityType.Series, "1");

    private static readonly MetadataGuid _other = new(TestSources.Plugin, MetadataEntityType.Series, "2");

    private static MetadataGuid StudioID(string id)
        => new(TestSources.Plugin, MetadataEntityType.Studio, id);

    private static MetadataGuid NetworkID(string id)
        => new(TestSources.Plugin, MetadataEntityType.Network, id);

    private sealed class Tables
    {
        public Metadata_StudioRepository Studios { get; }
            = CachedRepo.Build<Metadata_StudioRepository, int, Metadata_Studio>(row => row.Metadata_StudioID);

        public Metadata_Studio_EntryRepository StudioEntries { get; }
            = CachedRepo.Build<Metadata_Studio_EntryRepository, int, Metadata_Studio_Entry>(row => row.Metadata_Studio_EntryID);

        public Metadata_NetworkRepository Networks { get; }
            = CachedRepo.Build<Metadata_NetworkRepository, int, Metadata_Network>(row => row.Metadata_NetworkID);

        public Metadata_Network_EntryRepository NetworkEntries { get; }
            = CachedRepo.Build<Metadata_Network_EntryRepository, int, Metadata_Network_Entry>(row => row.Metadata_Network_EntryID);

        public TextCache Texts { get; } = new();

        public AnidbPeopleTables Anidb { get; } = new();

        public MetadataStudioStore Store()
        {
            var writer = new CacheOnlyRowWriter();
            return new(
                Studios,
                StudioEntries,
                Networks,
                NetworkEntries,
                writer,
                new MetadataTextStore(Texts, writer),
                Anidb.Anime,
                Anidb.Creators,
                Anidb.Staff
            );
        }

        public RepoFactoryScope Scope() => new RepoFactoryScope().Set(Studios).Set(Networks).Set(NetworkEntries);

        public Metadata_Studio Studio(string id) => Studios.GetByProviderID(TestSources.Plugin, id)!;

        public Metadata_Network Network(string id) => Networks.GetByProviderID(TestSources.Plugin, id)!;
    }

    #endregion

    #region Studios

    [Fact]
    public void AnEntrysStudiosReadBackInOrderWithTheirPart()
    {
        var tables = new Tables();
        using var scope = tables.Scope();
        var store = tables.Store();
        store.SaveStudios([
            new() { ID = StudioID("10"), Name = "Sunrise" },
            new() { ID = StudioID("11"), Name = "Aniplex" },
        ]);

        store.SetStudios(_series, [
            new() { StudioID = StudioID("11"), Type = StudioType.Production },
            new() { StudioID = StudioID("10"), Type = StudioType.Animation },
            new() { StudioID = StudioID("11"), Type = StudioType.Production },
        ]);

        var read = store.GetStudios(_series);
        Assert.Equal(["Aniplex", "Sunrise"], read.Select(studio => studio.Name));
        Assert.Equal([StudioType.Production, StudioType.Animation], read.Select(studio => studio.StudioType));
        Assert.Equal([_series], store.GetEntriesForStudio(StudioID("10")));
        Assert.Equal(StudioID("11"), read[0].ID);
        Assert.Equal(_series, ((IStudio<ISeries>)read[0]).ParentID);
        Assert.Equal(StudioID("10"), store.GetStudio(StudioID("10"))?.ID);

        store.SetStudios(_series, [new() { StudioID = StudioID("10") }]);

        Assert.Equal(["Sunrise"], store.GetStudios(_series).Select(studio => studio.Name));
        Assert.Empty(store.GetEntriesForStudio(StudioID("11")));
    }

    [Fact]
    public void AnidbStudiosReadFromTheAnidbCompaniesAndTheirStudioRoles()
    {
        var tables = new Tables();
        using var scope = tables.Anidb.Scope();
        tables.Anidb.Anime.Cache.Update(new AniDB_Anime { AniDB_AnimeID = 1, AnimeID = 30 });
        tables.Anidb.Creators.Cache.Update(new AniDB_Creator { AniDB_CreatorID = 1, CreatorID = 7, Name = "Sunrise", Type = CreatorType.Company });
        tables.Anidb.Creators.Cache.Update(new AniDB_Creator { AniDB_CreatorID = 2, CreatorID = 8, Name = "Kana", Type = CreatorType.Person });
        tables.Anidb.StaffRows.AddRange([
            new() { AnimeID = 31, CreatorID = 7, Role = "Animation Work", RoleType = CreatorRoleType.Studio },
            new() { AnimeID = 30, CreatorID = 7, Role = "Animation Work", RoleType = CreatorRoleType.Studio },
            new() { AnimeID = 30, CreatorID = 8, Role = "Direction", RoleType = CreatorRoleType.Director },
        ]);
        var store = tables.Store();
        var studio = new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Studio, "7");
        var series = new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, "30");

        Assert.Equal(studio, store.GetStudio(studio)?.ID);
        var read = Assert.Single(store.GetStudios(series));
        Assert.Equal(studio, read.ID);
        Assert.Equal(StudioType.Animation, read.StudioType);
        Assert.Equal([series, new(MetadataSource.AniDB, MetadataEntityType.Series, "31")], store.GetEntriesForStudio(studio));
        Assert.Empty(store.GetEntriesForStudio(new(MetadataSource.AniDB, MetadataEntityType.Studio, "8")));
        Assert.Null(store.GetStudio(new(MetadataSource.AniDB, MetadataEntityType.Studio, "9")));
    }

    #endregion

    #region Networks

    [Fact]
    public void AnEntrysNetworksReadBackInOrder()
    {
        var tables = new Tables();
        using var scope = tables.Scope();
        var store = tables.Store();
        store.SaveNetworks([new() { ID = NetworkID("n1"), Name = "Tokyo MX" }, new() { ID = NetworkID("n2"), Name = "Crunchyroll" }]);

        store.SetNetworks(_series, [NetworkID("n2"), NetworkID("n1"), NetworkID("n2")]);

        var read = store.GetNetworks(_series);
        Assert.Equal(["Crunchyroll", "Tokyo MX"], read.Select(network => network.Name));
        Assert.Equal([NetworkID("n2"), NetworkID("n1")], read.Select(network => network.ID));
        Assert.Equal([_series], store.GetEntriesForNetwork(NetworkID("n1")));
        Assert.Equal("Tokyo MX", store.GetNetwork(NetworkID("n1"))?.Name);
        Assert.Null(store.GetNetwork(StudioID("n1")));

        Assert.Equal(2, store.RemoveNetworks(_series));
        Assert.Empty(store.GetNetworks(_series));
    }

    [Fact]
    public void ANetworkOfAnotherKindIsRefused()
    {
        var tables = new Tables();
        using var scope = tables.Scope();
        var store = tables.Store();
        store.SaveNetworks([new() { ID = NetworkID("n1"), Name = "Tokyo MX" }]);
        store.SetNetworks(_series, [NetworkID("n1")]);

        Assert.Throws<ArgumentException>(() => store.SetNetworks(_series, [NetworkID("missing"), StudioID("n1")]));
        Assert.Equal([NetworkID("n1")], store.GetNetworks(_series).Select(network => network.ID));
        Assert.Null(store.GetNetwork(NetworkID("missing")));
    }

    #endregion

    #region Orphans

    [Fact]
    public void AStudioOrNetworkIsStampedWhenTheLastEntryLetsGoAndClearedWhenNamedAgain()
    {
        var tables = new Tables();
        using var scope = tables.Scope();
        var store = tables.Store();
        store.SaveStudios([new() { ID = StudioID("s1"), Name = "Sunrise" }]);
        store.SaveNetworks([new() { ID = NetworkID("n1"), Name = "Tokyo MX" }]);

        // Saved before any entry names them, they start out stamped.
        Assert.NotNull(tables.Studio("s1").LastOrphanedAt);
        Assert.NotNull(tables.Network("n1").LastOrphanedAt);

        store.SetStudios(_series, [new() { StudioID = StudioID("s1") }]);
        store.SetStudios(_other, [new() { StudioID = StudioID("s1") }]);
        store.SetNetworks(_series, [NetworkID("n1")]);
        Assert.Null(tables.Studio("s1").LastOrphanedAt);
        Assert.Null(tables.Network("n1").LastOrphanedAt);

        // Saving them again while named keeps them unstamped.
        store.SaveStudios([new() { ID = StudioID("s1"), Name = "Sunrise" }]);
        Assert.Null(tables.Studio("s1").LastOrphanedAt);

        store.RemoveStudios(_series);
        Assert.Null(tables.Studio("s1").LastOrphanedAt);
        store.SetStudios(_other, []);
        store.RemoveNetworks(_series);
        Assert.NotNull(tables.Studio("s1").LastOrphanedAt);
        Assert.NotNull(tables.Network("n1").LastOrphanedAt);
    }

    [Fact]
    public void OnlyTheStudiosAndNetworksOrphanedBeforeTheCutoffAreRemoved()
    {
        var tables = new Tables();
        using var scope = tables.Scope();
        var store = tables.Store();
        store.SaveStudios([new() { ID = StudioID("s1"), Name = "Used" }, new() { ID = StudioID("s2"), Name = "Unused" }]);
        store.SaveNetworks([new() { ID = NetworkID("n1"), Name = "Unused" }]);
        store.SetStudios(_series, [new() { StudioID = StudioID("s1") }]);

        // A cutoff before the stamps removes nothing.
        Assert.Empty(store.RemoveOrphaned(TestSources.Plugin, DateTime.Now.AddDays(-1)));
        Assert.NotNull(tables.Studio("s2"));

        var removed = store.RemoveOrphaned(TestSources.Plugin, DateTime.Now.AddMinutes(1));

        Assert.Equal([StudioID("s2"), NetworkID("n1")], removed);
        Assert.NotNull(store.GetStudio(StudioID("s1")));
        Assert.Null(store.GetStudio(StudioID("s2")));
        Assert.Null(store.GetNetwork(NetworkID("n1")));
    }

    [Fact]
    public void AnUnusedStudioWithoutAStampIsStampedRatherThanRemoved()
    {
        var tables = new Tables();
        using var scope = tables.Scope();
        var store = tables.Store();
        store.SaveStudios([new() { ID = StudioID("s1"), Name = "Old" }]);
        var unstamped = tables.Studio("s1");
        unstamped.LastOrphanedAt = null;

        Assert.Empty(store.RemoveOrphaned(TestSources.Plugin, DateTime.MaxValue));

        Assert.NotNull(tables.Studio("s1").LastOrphanedAt);
        Assert.Equal([StudioID("s1")], store.RemoveOrphaned(TestSources.Plugin, DateTime.MaxValue));
    }

    [Fact]
    public void RemovingTheOrphanedRaisesOneEventWithEveryOneRemoved()
    {
        var tables = new Tables();
        using var scope = tables.Scope();
        var store = tables.Store();
        var raised = new List<IReadOnlyList<MetadataGuid>>();
        store.EntitiesRemoved += (_, eventArgs) => raised.Add(eventArgs.EntityIDs);
        store.SaveStudios([new() { ID = StudioID("s1"), Name = "Unused" }]);
        store.SaveNetworks([new() { ID = NetworkID("n1"), Name = "Unused" }]);

        store.RemoveOrphaned(TestSources.Plugin, DateTime.MaxValue);
        store.RemoveOrphaned(TestSources.Plugin, DateTime.MaxValue);

        Assert.Equal([[StudioID("s1"), NetworkID("n1")]], raised);
    }

    #endregion
}
