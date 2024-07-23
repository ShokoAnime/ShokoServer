using System;

namespace Shoko.Plugin.Abstractions.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class RenamerAttribute : Attribute
    {
        public RenamerAttribute(string renamerId, string description = "")
        {
            RenamerId = renamerId;
            Description = description;
        }

        public string RenamerId { get; }

        public string Description { get; set; }
    }
}
