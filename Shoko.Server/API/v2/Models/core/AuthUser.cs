using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v2.Models.core
{
    public class AuthUser
    {
        [Required(ErrorMessage = "Username is required")]
        public string user { get; set; }

        [Required(ErrorMessage = "Password is required", AllowEmptyStrings = true)]
        public string pass { get; set; }

        [Required(ErrorMessage = "Device is required")]
        public string device { get; set; }
    }
}