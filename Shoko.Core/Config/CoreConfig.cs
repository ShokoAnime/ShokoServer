using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Shoko.Core.Config
{
    public class CoreConfig
    {
        [Required] public int ServerPort { get; set; } = 8111;
        [Required] public string JwtSecret { get; set; } = string.Empty;
        [Required] public string ConnectionString { get; set; } = "server=127.0.0.1;port=3306;uid=root;database=shoko_rewrite";
    }
}
