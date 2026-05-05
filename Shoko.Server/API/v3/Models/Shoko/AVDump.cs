using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko;

public static class AVDump
{
    public static class Input
    {
        public class DumpFilesBody
        {
            /// <summary>
            /// The file ids to add.
            /// </summary>
            /// <value></value>
            [Required, MinLength(1)]
            public List<int> FileIDs { get; set; } = [];

            /// <summary>
            /// Increase the priority for the command request.
            /// </summary>
            public bool Priority { get; set; } = false;
        }
    }
}
