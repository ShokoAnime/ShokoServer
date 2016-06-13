using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;
using NLog;

namespace JMMServer.Repositories
{
    public class AnimeGroupRepository
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public void Save(AnimeGroup obj)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    session.SaveOrUpdate(obj);
                    transaction.Commit();
                }
            }
            logger.Trace("Updating group stats by group from AnimeGroupRepository.Save: {0}", obj.AnimeGroupID);
            StatsCache.Instance.UpdateUsingGroup(obj.AnimeGroupID);
        }

        public AnimeGroup GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<AnimeGroup>(id);
            }
        }

        public AnimeGroup GetByID(ISession session, int id)
        {
            return session.Get<AnimeGroup>(id);
        }

        public List<AnimeGroup> GetByParentID(int parentid)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByParentID(session, parentid);
            }
        }

        public List<AnimeGroup> GetByParentID(ISession session, int parentid)
        {
            var grps = session
                .CreateCriteria(typeof(AnimeGroup))
                .Add(Restrictions.Eq("AnimeGroupParentID", parentid))
                .List<AnimeGroup>();

            return new List<AnimeGroup>(grps);
        }

        public List<AnimeGroup> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var grps = session
                    .CreateCriteria(typeof(AnimeGroup))
                    .List<AnimeGroup>();

                return new List<AnimeGroup>(grps);
            }
        }

        public List<AnimeGroup> GetAll(ISession session)
        {
            var grps = session
                .CreateCriteria(typeof(AnimeGroup))
                .List<AnimeGroup>();

            return new List<AnimeGroup>(grps);
        }

        public List<AnimeGroup> GetAllTopLevelGroups()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetAllTopLevelGroups(session);
            }
        }

        public List<AnimeGroup> GetAllTopLevelGroups(ISession session)
        {
            var grps = session
                .CreateCriteria(typeof(AnimeGroup))
                //.Add(Restrictions.Eq("AnimeGroupParentID", "null"))
                //.Add(Restrictions.IsEmpty("OrgUnits"))
                .Add(Restrictions.IsNull("AnimeGroupParentID"))
                .List<AnimeGroup>();

            return new List<AnimeGroup>(grps);
        }

        public void Delete(int id)
        {
            AnimeGroup cr = GetByID(id);
            if (cr != null)
            {
                // delete user records
                AnimeGroup_UserRepository repUsers = new AnimeGroup_UserRepository();
                foreach (AnimeGroup_User grpUser in repUsers.GetByGroupID(id))
                    repUsers.Delete(grpUser.AnimeGroup_UserID);
            }

            int parentID = 0;
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    if (cr != null)
                    {
                        if (cr.AnimeGroupParentID.HasValue) parentID = cr.AnimeGroupParentID.Value;
                        session.Delete(cr);
                        transaction.Commit();
                    }
                }
            }

            if (parentID > 0)
            {
                logger.Trace("Updating group stats by group from AnimeGroupRepository.Delete: {0}", parentID);
                StatsCache.Instance.UpdateUsingGroup(parentID);
            }
        }
    }
}