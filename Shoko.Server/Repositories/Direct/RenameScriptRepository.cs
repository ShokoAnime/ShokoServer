using System.Collections.Generic;
using System.Linq;
using NHibernate.Criterion;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct;

public class RenameScriptRepository : BaseDirectRepository<RenameScript, int>
{
    public RenameScript GetDefaultScript()
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var cr = session
                .Query<RenameScript>()
                .Where(a => a.IsEnabledOnImport == 1)
                .Take(1).SingleOrDefault();
            return cr;
        });
    }

    public RenameScript GetDefaultOrFirst()
    {
        return Lock(() =>
        {
            // This should list the enabled one first, falling back if none are
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session
                .Query<RenameScript>()
                .OrderByDescending(a => a.IsEnabledOnImport)
                .ThenBy(a => a.RenameScriptID)
                .Take(1).SingleOrDefault();
        });
    }

    public RenameScript GetByName(string scriptName)
    {
        return Lock(() =>
        {
            if (string.IsNullOrEmpty(scriptName))
            {
                return null;
            }

            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session
                .Query<RenameScript>()
                .Where(a => a.ScriptName == scriptName)
                .Take(1).SingleOrDefault();
        });
    }

    public List<RenameScript> GetByRenamerType(string renamerType)
    {
        if (string.IsNullOrEmpty(renamerType)) return new();
        using var session = DatabaseFactory.SessionFactory.OpenSession();
        var cr = session
            .CreateCriteria(typeof(RenameScript))
            .Add(Restrictions.Eq("RenamerType", renamerType))
            .List<RenameScript>()
            .ToList();
        return cr;
    }
}
