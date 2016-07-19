using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMContracts.API.Models
{
    /// <summary>
    /// Model that represents Users
    /// </summary>
    public class Users
    {
        /// <summary>
        /// Users list
        /// </summary>
        public List<User> users { get; set; }

        /// <summary>
        /// Users list
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ErrorString { get; set; }

        /// <summary>
        /// Contructor that create filter out of video
        /// </summary>
        public Users(JMMContracts.PlexAndKodi.PlexContract_Users users_)
        {
            ErrorString = users_.ErrorString;

            users = new List<User>();
            foreach (PlexAndKodi.PlexContract_User user_ in users_.Users)
            {
                users.Add(new User(user_));
            }
        }

        /// <summary>
        /// Parametraless constructor for XML Serialization
        /// </summary>
        public Users()
        {

        }
    }
}
