﻿using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Models.Server;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models;

public class SVR_Scan : Scan
{
    public string TitleText =>
        CreationTIme.ToString(CultureInfo.CurrentUICulture) + " (" + string.Join(" | ",
            this.GetImportFolderList()
                .Select(a => RepoFactory.ImportFolder.GetByID(a))
                .Where(a => a != null)
                .Select(a => a.ImportFolderLocation
                    .Split(
                        new[] { Path.PathSeparator, Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                        StringSplitOptions.RemoveEmptyEntries)
                    .LastOrDefault())
                .ToArray()) + ")";
}
