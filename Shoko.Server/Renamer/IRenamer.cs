using Shoko.Models.Server;
using Shoko.Server.Models;
using System;

namespace Shoko.Server.Renamer
{

    [Obsolete("Use plugin renamers instead")]
    public interface IRenamer
    {
        string GetFileName(SVR_VideoLocal_Place place);
        string GetFileName(SVR_VideoLocal video);

        (ImportFolder dest, string folder) GetDestinationFolder(SVR_VideoLocal_Place video);
    }
}