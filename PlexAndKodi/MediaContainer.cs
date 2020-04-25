using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using Nancy.Rest.Annotations.Atributes;

namespace Shoko.Models.PlexAndKodi
{
    [XmlType("MediaContainer")]
    [Serializable]
    [DataContract]
    [KnownType(typeof(Video))]
    [KnownType(typeof(Directory))]
 
    public class MediaContainer : Video
    {

        [XmlElement(typeof(Video), ElementName = "Video")]
        [XmlElement(typeof(Directory), ElementName = "Directory")]
        [DataMember(EmitDefaultValue = false, Order = 1)]
        public List<Video> Childrens { get; set; }

        [Tags("Plex")]
        [XmlAttribute("viewGroup")]
        [DataMember(EmitDefaultValue = false, Order = 2)]
        public string ViewGroup { get; set; }

        [Tags("Plex")]
        [XmlAttribute("viewMode")]
        [DataMember(EmitDefaultValue = false, Order = 3)]
        public string ViewMode { get; set; }

        [Tags("Plex")]
        [DataMember(EmitDefaultValue = false, Order = 4)]
        [XmlAttribute("contenttype")]
        public string ContentType { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 5)]
        [XmlAttribute("size")]
        public string Size { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 6)]
        [XmlAttribute("identifier")]
        public string Identifier { get; set; }

        [Tags("Plex")]
        [DataMember(EmitDefaultValue = false, Order = 7)]
        [XmlAttribute("mediaTagPrefix")]
        public string MediaTagPrefix { get; set; }

        [Tags("Plex")]
        [DataMember(EmitDefaultValue = false, Order = 8)]
        [XmlAttribute("mediaTagVersion")]
        public string MediaTagVersion { get; set; }

        [Tags("Plex")]
        [DataMember(EmitDefaultValue = false, Order = 9)]
        [XmlAttribute("allowSync")]
        public string AllowSync { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 10)]
        [XmlAttribute("totalSize")]
        public string TotalSize { get; set; }

        [Tags("Plex")]
        [DataMember(EmitDefaultValue = false, Order = 11)]
        [XmlAttribute("nocache")]
        public string NoCache { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 12)]
        [XmlAttribute("offset")]
        public string Offset { get; set; }

        [Tags("Plex")]
        [DataMember(EmitDefaultValue = false, Order = 13)]
        [XmlAttribute("librarySectionUUID")]
        public string LibrarySectionUUID { get; set; }

        [Tags("Plex")]
        [DataMember(EmitDefaultValue = false, Order = 14)]
        [XmlAttribute("librarySectionTitle")]
        public string LibrarySectionTitle { get; set; }

        [Tags("Plex")]
        [DataMember(EmitDefaultValue = false, Order = 15)]
        [XmlAttribute("librarySectionID")]
        public string LibrarySectionID { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 19)]
        [XmlAttribute("ErrorString")]
        public string ErrorString { get; set; }
    }
}