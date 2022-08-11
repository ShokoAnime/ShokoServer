using Newtonsoft.Json;
using Shoko.Models.Server;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text;
using System.Linq;
using Newtonsoft.Json.Converters;

namespace Shoko.Server.API.v3.Models.Shoko
{
    public class Renamer
    {
        public Renamer(string key, (Type type, string description) value)
        {
            var scripts = RepoFactory.RenameScript.GetRenamerType(key);
            ID = key;
            Description = value.description;
            Size = scripts.Count;
        }

        /// <summary>
        /// Renamer ID.
        /// </summary>
        public string ID;

        /// <summary>
        /// A short description about the renamer.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Number of pipeline stages using this renamer.
        /// </summary>
        public int Size { get; set; }

        public class SettingDescriptior
        {
            [JsonConverter(typeof(StringEnumConverter))]
            public SettingDescriptiorType Type { get; set; }

            public string Name { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]

            public string Label { get; set; }

            public string Category { get; set; }

            public SettingDescriptorValidation Validation { get; set; }
        }

        public class SettingDescriptorValidation
        {
            public bool Required { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public int? Max { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public int? Min { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public int? MaxLength { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public int? MinLength { get; set; }

            /// <summary>
            /// Forces the user to use one of the values in the list for the given type.
            /// </summary>
            /// <value></value>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public dynamic[] Values { get; set; }
        }

        public enum SettingDescriptiorType
        {
            Script,
            Boolean,
            Int,
            IntList,
            Long,
            LongList,
            Float,
            FloatList,
            String,
            StringList,
        }1
    }
}
