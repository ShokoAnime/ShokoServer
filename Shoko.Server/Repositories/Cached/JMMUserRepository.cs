using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Shoko.Models;
using Shoko.Models.Server;
using NHibernate;
using NLog;
using Shoko.Server.Databases;
using Shoko.Server.Entities;

namespace Shoko.Server.Repositories.Cached
{
    public class JMMUserRepository : BaseCachedRepository<SVR_JMMUser, int>
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private JMMUserRepository()
        {
            
        }

        public static JMMUserRepository Create()
        {
            return new JMMUserRepository();
        }

        protected override int SelectKey(SVR_JMMUser entity)
        {
            return entity.JMMUserID;
        }

        public override void PopulateIndexes()
        {

        }

        public override void RegenerateDb()
        {

        }

      

        //Disable base saves.
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("...", false)]
        public override void Save(SVR_JMMUser obj) { throw new NotSupportedException(); }
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("...", false)]
        public override void Save(IReadOnlyCollection<SVR_JMMUser> objs) { throw new NotSupportedException(); }

        public void Save(SVR_JMMUser obj, bool updateGroupFilters)
        {
            lock (obj)
            {
                bool isNew = false;
                if (obj.JMMUserID == 0)
                {
                    isNew = true;
                    base.Save(obj);
                }
                if (updateGroupFilters)
                {
                    using (var session = DatabaseFactory.SessionFactory.OpenSession())
                    {
                        SVR_JMMUser old = isNew ? null : session.Get<SVR_JMMUser>(obj.JMMUserID);
                        updateGroupFilters = SVR_JMMUser.CompareUser(old, obj);
                    }
                }
                base.Save(obj);
                if (updateGroupFilters)
                {
                    logger.Trace("Updating group filter stats by user from JMMUserRepository.Save: {0}", obj.JMMUserID);
                    obj.UpdateGroupFilters();
                }
            }
        }




        public SVR_JMMUser GetByUsername(string username)
        {
            try
            {
                return Cache.Values.First<SVR_JMMUser>(x => x.Username.ToLower() == username.ToLower());
            }
            catch
            {
                return null;
            }
            //foreach (JMMUser user in Cache.Values)
            //{
            //    if (user.UserName.ToLower() == username.ToLower())
            //        return user;
            //}
            //return null;
        }



        public List<SVR_JMMUser> GetAniDBUsers()
        {
            return Cache.Values.Where(a => a.IsAniDBUser == 1).ToList();
        }

        public List<SVR_JMMUser> GetTraktUsers()
        {
            return Cache.Values.Where(a => a.IsTraktUser == 1).ToList();
        }

        public SVR_JMMUser AuthenticateUser(string userName, string password)
        {
            string hashedPassword = Digest.Hash(password);
            return Cache.Values.FirstOrDefault(a => a.Username == userName && a.Password == hashedPassword);
        }

        public long GetTotalRecordCount()
        {
            return Cache.Keys.Count;
        }

    }
}