using System;

namespace Shoko.Models.Server
{
    public class CustomTag : ICloneable
    {
        public int CustomTagID { get; set; }
        public string TagName { get; set; }
        public string TagDescription { get; set; }

        public object Clone()
        {
            return new CustomTag
            {
                CustomTagID = CustomTagID,
                TagName = TagName,
                TagDescription = TagDescription
            };
        }
    }
}
