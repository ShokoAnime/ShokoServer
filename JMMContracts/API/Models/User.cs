using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMContracts.API.Models
{
    /// <summary>
    /// Model that represents User
    /// </summary>
    public class User
    {
        /// <summary>
        /// uniqe id of User
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// User display name
        /// </summary>
        public string name { get; set; }

        /// <summary>
        /// Contructor that create filter out of video
        /// </summary>
        public User(PlexAndKodi.PlexContract_User user)
        {
            id = user.id;
            name = user.name;
        }

        /// <summary>
        /// Parametraless constructor for XML Serialization
        /// </summary>
        public User()
        {

        }
    }
}
