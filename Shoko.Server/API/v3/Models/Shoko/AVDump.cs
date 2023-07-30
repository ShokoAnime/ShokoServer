
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3.Models.Shoko;

public static class AVDump
{
    public class Result
    {
        public string FullOutput { get; set; }

        public string Ed2k { get; set; }
    }

    public static class Input
    {
        public class DumpFilesBody
        {
            /// <summary>
            /// The file ids to add.
            /// </summary>
            /// <value></value>
            [MinLength(1)]
            public List<int> FileIDs { get; set; }

            /// <summary>
            /// Increase the priority for the command request.
            /// </summary>
            public  bool Priority { get; set; } = false;
        }
    }
}
