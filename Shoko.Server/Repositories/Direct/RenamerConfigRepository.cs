using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models;

#nullable enable
namespace Shoko.Server.Repositories.Direct;

public class RenamerConfigRepository : BaseDirectRepository<RenamerConfig, int>
{

    public RenamerConfig? GetByName(string? scriptName)
    {
        if (string.IsNullOrEmpty(scriptName))
            return null;

        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<RenamerConfig>()
                .Where(a => a.Name == scriptName)
                .Take(1)
                .SingleOrDefault();
        });
    }

    public RenamerConfigRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
