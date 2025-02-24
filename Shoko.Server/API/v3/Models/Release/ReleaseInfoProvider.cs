using System;
using System.ComponentModel.DataAnnotations;

#nullable enable
namespace Shoko.Server.API.v3.Models.Release;

public class ReleaseInfoProvider
{
    public required string Name { get; set; }

    public required Version Version { get; set; }

    public required bool IsEnabled { get; set; }

    public required int Priority { get; set; }

    public static class Input
    {
        public class UpdateMultipleProvidersBody
        {
            [Required]
            public string Name { get; set; } = string.Empty;

            public bool? IsEnabled { get; set; }

            public int? Priority { get; set; }
        }

        public class UpdateSingleProviderBody
        {
            public bool? IsEnabled { get; set; }

            public int? Priority { get; set; }
        }
    }
}
