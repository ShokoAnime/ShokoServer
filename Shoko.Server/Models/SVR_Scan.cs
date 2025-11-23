using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models;

public class SVR_Scan : Scan
{
    public string TitleText =>
        CreationTIme.ToString(CultureInfo.CurrentUICulture) + " (" + string.Join(" | ",
            this.ImportFolders.Split(',')
                .Select(int.Parse)
                .Select(RepoFactory.ShokoManagedFolder.GetByID)
                .WhereNotNull()
                .Select(a => a.Path
                    .Split(
                        new[] { Path.PathSeparator, Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                        StringSplitOptions.RemoveEmptyEntries)
                    .LastOrDefault())
                .ToArray()) + ")";
}
