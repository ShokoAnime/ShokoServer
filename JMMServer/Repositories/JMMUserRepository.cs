using System;
using System.Collections.Generic;
using System.Linq;
using JMMContracts;
using JMMServer.Databases;
using JMMServer.Entities;
using NHibernate;
using NLog;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories
{
    public class JMMUserRepository
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private static PocoCache<int, JMMUser> Cache;

        public static void InitCache()
        {
            string t = "Users";
            ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, t, string.Empty);
            JMMUserRepository repo = new JMMUserRepository();
            Cache = new PocoCache<int, JMMUser>(repo.InternalGetAll(), a => a.JMMUserID);
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
                contract.HideCategories=new HashSet<string>();

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
                contract.PlexUsers=new HashSet<string>();
            }
            user.Contract = contract;
        }

        private List<JMMUser> InternalGetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(JMMUser))
                    .List<JMMUser>();

                return new List<JMMUser>(objs);
            }
        }

        public void Save(JMMUser obj, bool updateGroupFilters)
        {
            lock (obj)
            {
                bool isNew = false;
                if (obj.JMMUserID == 0)
                {
                    isNew = true;
                    using (var session = JMMService.SessionFactory.OpenSession())
                    {
                        obj.Contract = null;
                        using (var transaction = session.BeginTransaction())
                        {
                            session.SaveOrUpdate(obj);
                            transaction.Commit();
                        }
                    }
                }
                GenerateContract(obj);
                if (updateGroupFilters)
                {
                    using (var session = JMMService.SessionFactory.OpenSession())
                    {
                        JMMUser old = isNew ? null : session.Get<JMMUser>(obj.JMMUserID);
                        updateGroupFilters = JMMUser.CompareUser(old?.Contract, obj.Contract);
                    }
                }
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    // populate the database
                    using (var transaction = session.BeginTransaction())
                    {
                        session.SaveOrUpdate(obj);
                        transaction.Commit();
                    }
                }
                Cache.Update(obj);
                if (updateGroupFilters)
                {
                    logger.Trace("Updating group filter stats by user from JMMUserRepository.Save: {0}", obj.JMMUserID);
                    obj.UpdateGroupFilters();
                }
            }
        }

        public JMMUser GetByID(int id)
        {
            return Cache.Get(id);
        }

        public JMMUser GetByID(ISession session, int id)
        {
            return Cache.Get(id);
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

        public List<JMMUser> GetAll()
        {
            return Cache.Values.ToList();
        }

        public List<JMMUser> GetAll(ISession session)
        {
            return Cache.Values.ToList();
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


        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    JMMUser cr = GetByID(id);
                    if (cr != null)
                    {
                        Cache.Remove(cr);
                        session.Delete(cr);
                        transaction.Commit();
                    }
                }
            }
        }
    }
}