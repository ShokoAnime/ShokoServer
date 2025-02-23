using System;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace Shoko.Models.PlexAndKodi
{
    [Serializable]
    [DataContract]
    public class Tag
    {
        [DataMember(EmitDefaultValue = false, Order = 1)]
        [XmlAttribute("tag")]
        public string Value { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 2)]
        [XmlAttribute("role")]
        public string Role { get; set; }
        // Override for ease of making sets
        protected bool Equals(Tag other)
        {
            return string.Equals(Value, other.Value) && string.Equals(Role, other.Role);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Tag) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Value != null ? Value.GetHashCode() : 0) * 397) ^ (Role != null ? Role.GetHashCode() : 0);
            }
        }
    }
}