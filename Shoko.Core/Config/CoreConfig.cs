using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Shoko.Core.Config
{
    public class CoreConfig
    {
        [Required] public int ServerPort { get; set; } = 8111;
    }
}
