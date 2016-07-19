using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMContracts.PlexAndKodi;

namespace JMMContracts.API.Models
{
    /// <summary>
    /// Model that represends RoleTag
    /// </summary>
    public class RoleTag
    {
        /// <summary>
        /// Actor name
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string seiyuu { get; set; }
       
        /// <summary>
        /// Actor picture
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string seiyuuPic { get; set; }
        
        /// <summary>
        /// Role which played actor
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string role { get; set; }

        /// <summary>
        /// Role description
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string roleDesc { get; set; }

        /// <summary>
        /// Role picture
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string rolePic { get; set; }


        public RoleTag()
        {

        }

        /// <summary>
        /// Contructor that create Metadata_Episode out of video
        /// </summary>
        public RoleTag(PlexAndKodi.RoleTag roletag)
        {
            role = roletag.Role;
            seiyuu = roletag.Value;
            roleDesc = roletag.RoleDescription;
            rolePic = roletag.RolePicture;
            seiyuuPic = roletag.TagPicture;
        }

        public static explicit operator RoleTag(PlexAndKodi.RoleTag role_in)
        {
            RoleTag role_out = new RoleTag();

            role_out.role = role_in.Role;
            role_out.roleDesc = role_in.RoleDescription;
            role_out.rolePic = role_in.RolePicture;
            role_out.seiyuu = role_in.Value;
            role_out.seiyuuPic = role_in.TagPicture;

            return role_out;
        }
    }
}
