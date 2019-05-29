using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Shoko.Core.API.Models
{
    public class AuthenticationModel
    {
        [Required] public string Username { get; set; }

        public string Password { get; set; }

    }
}
