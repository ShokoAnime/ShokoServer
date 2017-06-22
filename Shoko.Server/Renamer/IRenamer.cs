using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;

namespace Shoko.Server.Renamer
{
    public interface IRenamer
    {
        string GetFileName(SVR_VideoLocal_Place video);

        (ImportFolder dest, string folder) GetDestinationFolder(SVR_VideoLocal_Place video);
    }
}