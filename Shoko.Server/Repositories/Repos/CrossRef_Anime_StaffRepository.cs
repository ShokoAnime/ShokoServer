using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;

namespace Shoko.Server.Repositories.Repos
{
    public class CrossRef_Anime_StaffRepository : BaseRepository<CrossRef_Anime_Staff, int>
    {
        private PocoIndex<int, CrossRef_Anime_Staff, int> AnimeIDs;
        private PocoIndex<int, CrossRef_Anime_Staff, int> StaffIDs;
        private PocoIndex<int, CrossRef_Anime_Staff, int?> RoleIDs;
        private PocoIndex<int, CrossRef_Anime_Staff, StaffRoleType> RoleTypes;

        private static readonly Dictionary<string, CharacterAppearanceType> Roles =
            new Dictionary<string, CharacterAppearanceType>
            {
                {"main character in", CharacterAppearanceType.Main_Character},
                {"secondary cast in", CharacterAppearanceType.Minor_Character},
                {"appears in", CharacterAppearanceType.Background_Character},
                {"cameo appearance in", CharacterAppearanceType.Cameo},
            };

        internal override void PopulateIndexes()
        {
            AnimeIDs = new PocoIndex<int, CrossRef_Anime_Staff, int>(Cache, a => a.AniDB_AnimeID);
            StaffIDs = new PocoIndex<int, CrossRef_Anime_Staff, int>(Cache, a => a.StaffID);
            RoleIDs = new PocoIndex<int, CrossRef_Anime_Staff, int?>(Cache, a => a.RoleID);
            RoleTypes = new PocoIndex<int, CrossRef_Anime_Staff, StaffRoleType>(Cache, a => (StaffRoleType) a.RoleType);
        }

        public void RegenerateDb()
        {
            int i = 0;

            using (var list = BeginBatchUpdate(() => Cache.Values.Where(animeStaff => animeStaff.RoleID != null && Roles.ContainsKey(animeStaff.Role)).ToList()))
            {
                foreach (var animeStaff in list)
                {
                    animeStaff.Role = Roles[animeStaff.Role].ToString().Replace("_", " ");
                    //Save(animeStaff);
                    i++;
                    if (i % 10 == 0)
                        ServerState.Instance.CurrentSetupStatus = string.Format(
                            Commons.Properties.Resources.Database_Validating, typeof(CrossRef_Anime_Staff).Name,
                            $" DbRegen - {i}/{list.Count()}");
                }
                list.Commit();
            }
        }

        internal override int SelectKey(CrossRef_Anime_Staff entity)
        {
            return entity.CrossRef_Anime_StaffID;
        }

        public List<CrossRef_Anime_Staff> GetByStaffID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return StaffIDs.GetMultiple(id);
                return Table.Where(s => s.StaffID == id).ToList();
            }
        }

        public List<CrossRef_Anime_Staff> GetByRoleID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return RoleIDs.GetMultiple(id);
                return Table.Where(s => s.RoleID == id).ToList();
            }
        }

        public List<CrossRef_Anime_Staff> GetByRoleType(StaffRoleType type)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return RoleTypes.GetMultiple(type);
                return Table.Where(s => (StaffRoleType)s.RoleType == type).ToList();
            }
        }

        public List<CrossRef_Anime_Staff> GetByAnimeID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return AnimeIDs.GetMultiple(id);
                return Table.Where(s => s.AniDB_AnimeID == id).ToList();
            }
        }

        public List<CrossRef_Anime_Staff> GetByAnimeIDAndRoleType(int id, StaffRoleType type)
        {
            using (RepoLock.ReaderLock())
            {
                return GetByAnimeID(id).Where(xref => xref.RoleType == (int) type).ToList();
            }
        }

        public CrossRef_Anime_Staff GetByParts(int AnimeID, int? RoleID, int StaffID, StaffRoleType RoleType)
        {
            using (RepoLock.ReaderLock())
            {
                return GetByAnimeID(AnimeID).Find(a =>
                    a.RoleID == RoleID && a.StaffID == StaffID && a.RoleType == (int) RoleType);
            }
        }

        internal override void ClearIndexes()
        {
            AnimeIDs = null;
            StaffIDs = null;
            RoleIDs = null;
            RoleTypes = null;
        }
    }
}
