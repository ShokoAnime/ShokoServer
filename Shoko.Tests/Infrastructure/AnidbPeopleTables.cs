using System.Collections.Generic;
using System.Linq;
using Moq;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Direct;

namespace Shoko.Tests.Infrastructure;

/// <summary>
/// The AniDB tables the people and studio stores read for <c>anidb</c> IDs, in
/// memory and empty until a test adds rows. The staff roles live in
/// <see cref="StaffRows"/>, read through a mocked direct repository.
/// </summary>
/// <remarks>
/// The AniDB models read each other through the <c>RepoFactory</c> statics,
/// so a test reading credits or studios opens <see cref="Scope"/>.
/// </remarks>
public sealed class AnidbPeopleTables
{
    public AniDB_AnimeRepository Anime { get; }
        = CachedRepo.Build<AniDB_AnimeRepository, int, AniDB_Anime>(anime => anime.AniDB_AnimeID);

    public AniDB_EpisodeRepository Episodes { get; }
        = CachedRepo.Build<AniDB_EpisodeRepository, int, AniDB_Episode>(episode => episode.AniDB_EpisodeID);

    public AniDB_CreatorRepository Creators { get; }
        = CachedRepo.Build<AniDB_CreatorRepository, int, AniDB_Creator>(creator => creator.AniDB_CreatorID);

    public AniDB_CharacterRepository Characters { get; }
        = CachedRepo.Build<AniDB_CharacterRepository, int, AniDB_Character>(character => character.AniDB_CharacterID);

    public AniDB_Anime_CharacterRepository AnimeCharacters { get; }
        = CachedRepo.Build<AniDB_Anime_CharacterRepository, int, AniDB_Anime_Character>(xref => xref.AniDB_Anime_CharacterID);

    public AniDB_Anime_Character_CreatorRepository CharacterCreators { get; }
        = CachedRepo.Build<AniDB_Anime_Character_CreatorRepository, int, AniDB_Anime_Character_Creator>(xref => xref.AniDB_Anime_Character_CreatorID);

    /// <summary>
    /// The staff roles the mocked <see cref="Staff"/> repository answers from.
    /// </summary>
    public List<AniDB_Anime_Staff> StaffRows { get; } = [];

    /// <summary>
    /// The staff roles, read from <see cref="StaffRows"/>.
    /// </summary>
    public AniDB_Anime_StaffRepository Staff { get; }

    public AnidbPeopleTables()
    {
        var staff = new Mock<AniDB_Anime_StaffRepository>(new object[1]);
        staff.Setup(repository => repository.GetByAnimeID(It.IsAny<int>()))
            .Returns((int animeID) => [.. StaffRows.Where(xref => xref.AnimeID == animeID)]);
        staff.Setup(repository => repository.GetByCreatorID(It.IsAny<int>()))
            .Returns((int creatorID) => [.. StaffRows.Where(xref => xref.CreatorID == creatorID)]);
        Staff = staff.Object;
    }

    /// <summary>
    /// Installs the tables into the <c>RepoFactory</c> statics.
    /// </summary>
    /// <returns>The scope, which puts the statics back when disposed.</returns>
    public RepoFactoryScope Scope()
        => new RepoFactoryScope()
            .Set(Anime)
            .Set(Episodes)
            .Set(Creators)
            .Set(Characters)
            .Set(AnimeCharacters)
            .Set(CharacterCreators)
            .Set(Staff);
}
