using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models;

#nullable enable
namespace Shoko.Server.Repositories.Direct;

public class RenamerInstanceRepository : BaseDirectRepository<RenameScript, int>
{

    public RenamerInstance? GetByName(string? scriptName)
    {
        if (string.IsNullOrEmpty(scriptName))
            return null;

        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<RenamerInstance>()
                .Where(a => a.Name == scriptName)
                .Take(1)
                .SingleOrDefault();
        });
    }

    public List<RenamerInstance> GetByType(Type renamerType)
    {
        using var session = _databaseFactory.SessionFactory.OpenSession();
        var cr = session
            .Query<RenamerInstance>()
            .Where(a => a.Type == renamerType)
            .ToList();
        return cr;
    }

    public RenamerInstanceRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
