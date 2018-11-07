using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Shoko.Server.Models;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;
using Shoko.Server.Repositories.Cache;

namespace Shoko.Server.Repositories.Repos
{
    public class JMMUserRepository : BaseRepository<SVR_JMMUser, int, bool>
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();


        public static JMMUserRepository Create() => new JMMUserRepository();

        internal override int SelectKey(SVR_JMMUser entity) => entity.JMMUserID;

        internal override void PopulateIndexes()
        {
        }

        internal override void ClearIndexes()
        {
        }

        internal override object BeginSave(SVR_JMMUser entity, SVR_JMMUser original_entity, bool updateGroupFilters)
        {
            bool ret=updateGroupFilters;
            if (updateGroupFilters)
                ret = SVR_JMMUser.CompareUser(original_entity, entity);
            return ret;
        }

        internal override void EndSave(SVR_JMMUser entity, object returnFromBeginSave, bool parameters)
        {
            bool ret = (bool) returnFromBeginSave;
            if (ret)
            {
                logger.Trace("Updating group filter stats by user from JMMUserRepository.Save: {0}", entity.JMMUserID);
                entity.UpdateGroupFilters();
            }
        }




        public SVR_JMMUser GetByUsername(string username)
        {
            return Where(x =>
                x.Username.Equals(username, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
        }


        public List<SVR_JMMUser> GetAniDBUsers()
        {
            return Where(a => a.IsAniDBUser == 1).ToList();
        }

        public List<SVR_JMMUser> GetTraktUsers()
        {
            return Where(a => a.IsTraktUser == 1).ToList();
        }

        public SVR_JMMUser AuthenticateUser(string userName, string password)
        {
            if (password == null) password = string.Empty;
            string hashedPassword = Digest.Hash(password);
            return Where(a =>
                a.Username.Equals(userName, StringComparison.InvariantCultureIgnoreCase) &&
                a.Password.Equals(hashedPassword)).FirstOrDefault();
        }

        public long GetTotalRecordCount()
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Cache.Keys.Count;
                return Table.Count();
            }
        }
    }
}