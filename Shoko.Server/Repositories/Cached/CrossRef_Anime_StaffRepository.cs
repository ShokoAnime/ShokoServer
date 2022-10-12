﻿using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Properties;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Server;

namespace Shoko.Server.Repositories.Cached;

public class CrossRef_Anime_StaffRepository : BaseCachedRepository<CrossRef_Anime_Staff, int>
{
    private PocoIndex<int, CrossRef_Anime_Staff, int> AnimeIDs;
    private PocoIndex<int, CrossRef_Anime_Staff, int> StaffIDs;
    private PocoIndex<int, CrossRef_Anime_Staff, int?> RoleIDs;
    private PocoIndex<int, CrossRef_Anime_Staff, StaffRoleType> RoleTypes;

    public static readonly Dictionary<string, CharacterAppearanceType> Roles =
        new()
        {
            { "main character in", CharacterAppearanceType.Main_Character },
            { "secondary cast in", CharacterAppearanceType.Minor_Character },
            { "appears in", CharacterAppearanceType.Background_Character },
            { "cameo appearance in", CharacterAppearanceType.Cameo }
        };

    public override void PopulateIndexes()
    {
        AnimeIDs = new PocoIndex<int, CrossRef_Anime_Staff, int>(Cache, a => a.AniDB_AnimeID);
        StaffIDs = new PocoIndex<int, CrossRef_Anime_Staff, int>(Cache, a => a.StaffID);
        RoleIDs = new PocoIndex<int, CrossRef_Anime_Staff, int?>(Cache, a => a.RoleID);
        RoleTypes = new PocoIndex<int, CrossRef_Anime_Staff, StaffRoleType>(Cache, a => (StaffRoleType)a.RoleType);
    }

    public override void RegenerateDb()
    {
        var list = Cache.Values.Where(animeStaff =>
            animeStaff.RoleID != null && !string.IsNullOrEmpty(animeStaff.Role) && Roles.ContainsKey(animeStaff.Role) &&
            Roles[animeStaff.Role].ToString().Contains("_")).ToList();
        for (var index = 0; index < list.Count; index++)
        {
            var animeStaff = list[index];
            animeStaff.Role = Roles[animeStaff.Role].ToString().Replace("_", " ");
            Save(animeStaff);
            if (index % 10 == 0)
            {
                ServerState.Instance.ServerStartingStatus = string.Format(
                    Resources.Database_Validating, nameof(CrossRef_Anime_Staff),
                    $" DbRegen - {index}/{list.Count}"
                );
            }
        }
    }

    protected override int SelectKey(CrossRef_Anime_Staff entity)
    {
        return entity.CrossRef_Anime_StaffID;
    }

    public List<CrossRef_Anime_Staff> GetByStaffID(int id)
    {
        return ReadLock(() => StaffIDs.GetMultiple(id));
    }

    public List<CrossRef_Anime_Staff> GetByRoleID(int id)
    {
        return ReadLock(() => RoleIDs.GetMultiple(id));
    }

    public List<CrossRef_Anime_Staff> GetByRoleType(StaffRoleType type)
    {
        return ReadLock(() => RoleTypes.GetMultiple(type));
    }

    public List<CrossRef_Anime_Staff> GetByAnimeID(int id)
    {
        return ReadLock(() => AnimeIDs.GetMultiple(id));
    }

    public List<CrossRef_Anime_Staff> GetByAnimeIDAndRoleType(int id, StaffRoleType type)
    {
        return GetByAnimeID(id).Where(xref => xref.RoleType == (int)type).ToList();
    }

    public CrossRef_Anime_Staff GetByParts(int AnimeID, int? RoleID, int StaffID, StaffRoleType RoleType)
    {
        return GetByAnimeID(AnimeID).FirstOrDefault(a =>
            a.RoleID == RoleID && a.StaffID == StaffID && a.RoleType == (int)RoleType);
    }
}
