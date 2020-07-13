using System.ComponentModel;
using Newtonsoft.Json;

namespace Shoko.Server.API.v3.Models.Shoko
{
    public class Folder
    {
        public string Path { get; set; }
        
        [DefaultValue(false)]            
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool CanAccess { get; set; }
        public ChildItems Sizes { get; set; }
    }
}