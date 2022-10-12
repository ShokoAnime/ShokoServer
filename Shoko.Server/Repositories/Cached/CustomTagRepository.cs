﻿using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories;

public class CustomTagRepository : BaseCachedRepository<CustomTag, int>
{
    public CustomTagRepository()
    {
        DeleteWithOpenTransactionCallback = (ses, obj) =>
        {
            RepoFactory.CrossRef_CustomTag.DeleteWithOpenTransaction(ses,
                RepoFactory.CrossRef_CustomTag.GetByCustomTagID(obj.CustomTagID));
        };
    }

    protected override int SelectKey(CustomTag entity)
    {
        return entity.CustomTagID;
    }

    public override void PopulateIndexes()
    {
    }

    public override void RegenerateDb()
    {
    }

    public List<CustomTag> GetByAnimeID(int animeID)
    {
        return RepoFactory.CrossRef_CustomTag.GetByAnimeID(animeID)
            .Select(a => GetByID(a.CustomTagID))
            .Where(a => a != null)
            .ToList();
    }


    public Dictionary<int, List<CustomTag>> GetByAnimeIDs(ISessionWrapper session, int[] animeIDs)
    {
        return animeIDs.ToDictionary(a => a,
            a => RepoFactory.CrossRef_CustomTag.GetByAnimeID(a)
                .Select(b => GetByID(b.CustomTagID))
                .Where(b => b != null)
                .ToList());
    }
}
