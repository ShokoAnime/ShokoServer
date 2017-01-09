using Shoko.Models.Server;

namespace Shoko.Models.Client
{
    public class CL_ImportFolder_Save_Response
    {
        public string ErrorMessage { get; set; }
        public ImportFolder ImportFolder { get; set; }
    }
}