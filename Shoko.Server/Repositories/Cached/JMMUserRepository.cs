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
    public class JMMUserRepository : BaseCachedRepository<JMMUser, int>
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private JMMUserRepository()
        {
            
        }

        public static JMMUserRepository Create()
        {
            return new JMMUserRepository();
        }

        protected override int SelectKey(JMMUser entity)
        {
            return entity.JMMUserID;
        }

        public override void PopulateIndexes()
        {

        }

        public override void RegenerateDb()
        {

        }

        public static void GenerateContract(JMMUser user)
        {
            Contract_JMMUser contract = new Contract_JMMUser();
            contract.JMMUserID = user.JMMUserID;
            contract.Username = user.Username;
            contract.Password = user.Password;
            contract.IsAdmin = user.IsAdmin;
            contract.IsAniDBUser = user.IsAniDBUser;
            contract.IsTraktUser = user.IsTraktUser;
            if (!string.IsNullOrEmpty(user.HideCategories))
            {
                contract.HideCategories =
                    new HashSet<string>(
                        user.HideCategories.Trim().Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries)
                            .Select(a => a.Trim())
                            .Where(a => !string.IsNullOrEmpty(a)).Distinct(StringComparer.InvariantCultureIgnoreCase),
                        StringComparer.InvariantCultureIgnoreCase);
            }
            else
                contract.HideCategories=new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            contract.CanEditServerSettings = user.CanEditServerSettings;
            if (!string.IsNullOrEmpty(user.PlexUsers))
            {
                contract.PlexUsers =
                    new HashSet<string>(
                        user.PlexUsers.Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries)
                            .Select(a => a.Trim())
                            .Where(a => !string.IsNullOrEmpty(a)).Distinct(StringComparer.InvariantCultureIgnoreCase),
                        StringComparer.InvariantCultureIgnoreCase);
            }
            else
            {
                contract.PlexUsers=new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            }
            user.Contract = contract;
        }


        //Disable base saves.
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("...", false)]
        public override void Save(JMMUser obj) { throw new NotSupportedException(); }
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("...", false)]
        public override void Save(IReadOnlyCollection<JMMUser> objs) { throw new NotSupportedException(); }

        public void Save(JMMUser obj, bool updateGroupFilters)
        {
            lock (obj)
            {
                bool isNew = false;
                if (obj.JMMUserID == 0)
                {
                    isNew = true;
                    obj.Contract = null;
                    base.Save(obj);
                }
                GenerateContract(obj);
                if (updateGroupFilters)
                {
                    using (var session = DatabaseFactory.SessionFactory.OpenSession())
                    {
                        JMMUser old = isNew ? null : session.Get<JMMUser>(obj.JMMUserID);
                        updateGroupFilters = JMMUser.CompareUser(old?.Contract, obj.Contract);
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




        public JMMUser GetByUsername(string username)
        {
            try
            {
                return Cache.Values.First<JMMUser>(x => x.Username.ToLower() == username.ToLower());
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



        public List<JMMUser> GetAniDBUsers()
        {
            return Cache.Values.Where(a => a.IsAniDBUser == 1).ToList();
        }

        public List<JMMUser> GetTraktUsers()
        {
            return Cache.Values.Where(a => a.IsTraktUser == 1).ToList();
        }

        public JMMUser AuthenticateUser(string userName, string password)
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