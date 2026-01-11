using System;
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Models.Server
{
    public class CustomTag : ICloneable, ITag
    {
        public int CustomTagID { get; set; }
        public string TagName { get; set; }
        public string TagDescription { get; set; }
        public bool Spoiler { get; set; }

        public object Clone()
        {
            return new CustomTag
            {
                CustomTagID = CustomTagID,
                TagName = TagName,
                TagDescription = TagDescription
            };
        }

        public string Name => TagName;
        public string Description => TagDescription;
        bool ITag.Spoiler => Spoiler;
    }
}
