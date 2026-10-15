using System.Collections.Generic;
using System.Reflection;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers that a row saved or removed through its repository outdates what the text manager
/// worked out from it, with no write path telling the manager itself: plugin and AniDB rows
/// read by their IDs, and the collection rows a series may be named by.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class TextRepositoryInvalidationTests
{
    #region Helpers

    private static readonly MetadataGuid _studio = new(TestSources.Plugin, MetadataEntityType.Studio, "s5");

    private static readonly MetadataGuid _character = new(MetadataSource.AniDB, MetadataEntityType.Character, "7");

    /// <summary>
    /// A text manager put in place for the repositories to tell, over a cache-only store, and the
    /// entries it forgot.
    /// </summary>
    private sealed class Harness : System.IDisposable
    {
        private readonly RepoFactoryScope _scope;

        public Mock<IMetadataService> Service { get; } = new();

        public MetadataTextStore Store { get; } = new(new TextCache(), new CacheOnlyRowWriter());

        public MetadataTextManager Manager { get; }

        public List<MetadataGuid> Forgotten { get; } = [];

        public Harness(System.Func<RepoFactoryScope, RepoFactoryScope>? setup = null)
        {
            Manager = TestTextManager.Build(Store, Service.Object);
            Manager.Forgotten += Forgotten.Add;
            _scope = new RepoFactoryScope().Set(Manager);
            setup?.Invoke(_scope);
        }

        public void Holds(MetadataGuid id, IMetadata? entry)
            => Service.Setup(service => service.GetEntry(id)).Returns(entry);

        public void Dispose()
            => _scope.Dispose();
    }

    /// <summary>
    /// Puts a saved row in a repository's cache the way a committed save does.
    /// </summary>
    /// <param name="repository">The repository.</param>
    /// <param name="row">The row.</param>
    private static void AfterSave<T>(object repository, T row)
        => Invoke(repository, "UpdateCache", row);

    /// <summary>
    /// Takes a removed row out of a repository's cache the way a committed delete does.
    /// </summary>
    /// <param name="repository">The repository.</param>
    /// <param name="row">The row.</param>
    private static void AfterRemove<T>(object repository, T row)
        => Invoke(repository, "DeleteFromCache", row);

    private static void Invoke<T>(object repository, string name, T row)
        => repository.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic, [typeof(T)])!.Invoke(repository, [row]);

    #endregion

    [Fact]
    public void APluginRowWrittenWithUnchangedTextsOutdatesItsTitle()
    {
        using var harness = new Harness();
        var studios = CachedRepo.Build<Metadata_StudioRepository, int, Metadata_Studio>(row => row.Metadata_StudioID);
        var studio = new Metadata_Studio { Source = TestSources.Plugin, ProviderID = "s5", Name = "Old Name" };
        harness.Holds(_studio, studio);
        harness.Store.WriteWithoutEntries([], new MetadataRowChanges<Metadata_Studio>(studios, [studio], []));
        Assert.Equal("Old Name", harness.Manager.GetPreferredTitle(_studio)?.Value);

        // Only the name on the row changes; no text is written.
        studio.Name = "New Name";
        harness.Store.WriteWithoutEntries([], new MetadataRowChanges<Metadata_Studio>(studios, [studio], []));

        Assert.Equal("New Name", harness.Manager.GetPreferredTitle(_studio)?.Value);
    }

    [Fact]
    public void APluginRowRemovedThroughItsRepositoryIsNoLongerNamed()
    {
        using var harness = new Harness();
        var studios = CachedRepo.Build<Metadata_StudioRepository, int, Metadata_Studio>(row => row.Metadata_StudioID);
        var studio = new Metadata_Studio { Source = TestSources.Plugin, ProviderID = "s5", Name = "Gone Soon" };
        harness.Holds(_studio, studio);
        harness.Store.WriteWithoutEntries([], new MetadataRowChanges<Metadata_Studio>(studios, [studio], []));
        Assert.Equal("Gone Soon", harness.Manager.GetPreferredTitle(_studio)?.Value);

        harness.Holds(_studio, null);
        harness.Store.WriteWithoutEntries([_studio], new MetadataRowChanges<Metadata_Studio>(studios, [], [studio]));

        Assert.Null(harness.Manager.GetPreferredTitle(_studio));
    }

    [Fact]
    public void AnAnidbRowSavedThroughItsRepositoryOutdatesItsOverview()
    {
        using var harness = new Harness();
        var character = new AniDB_Character { AniDB_CharacterID = 1, CharacterID = 7, Description = "Before." };
        var characters = CachedRepo.Build<AniDB_CharacterRepository, int, AniDB_Character>(row => row.AniDB_CharacterID, character);
        harness.Holds(_character, character);
        Assert.Equal("Before.", harness.Manager.GetPreferredOverview(_character)?.Value);

        character.Description = "After.";
        AfterSave(characters, character);

        Assert.Equal("After.", harness.Manager.GetPreferredOverview(_character)?.Value);
    }

    [Fact]
    public void ACollectionMemberSavedOrRemovedForgetsTheMemberAndTheCollection()
    {
        using var harness = new Harness();
        var members = CachedRepo.Build<Metadata_Collection_MemberRepository, int, Metadata_Collection_Member>(row => row.Metadata_Collection_MemberID);
        var member = new Metadata_Collection_Member
        {
            Metadata_Collection_MemberID = 1,
            Source = TestSources.Plugin,
            CollectionID = "franchise",
            MemberType = MetadataEntityType.Movie,
            MemberID = "film",
        };

        AfterSave(members, member);
        Assert.Contains(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Movie, "film"), harness.Forgotten);
        Assert.Contains(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Collection, "franchise"), harness.Forgotten);

        harness.Forgotten.Clear();
        AfterRemove(members, member);
        Assert.Contains(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Movie, "film"), harness.Forgotten);
        Assert.Contains(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Collection, "franchise"), harness.Forgotten);
    }
}
