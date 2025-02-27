using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

#nullable enable
namespace Shoko.Server.API.v3.Models.Hashing;

public class HashProvider
{
    public required Guid ID { get; set; }

    public required string Name { get; set; }

    public required Version Version { get; set; }

    public required HashSet<string> EnabledHashTypes { get; set; }

    public required int Priority { get; set; }

    public static class Input
    {
        public class UpdateMultipleProvidersBody
        {
            [Required]
            public Guid ID { get; set; }

            public HashSet<string>? EnabledHashTypes { get; set; }

            public int? Priority { get; set; }
        }

        public class UpdateSingleProviderBody
        {
            public HashSet<string>? EnabledHashTypes { get; set; }

            public int? Priority { get; set; }
        }
    }
}
