using System;

namespace Shoko.Plugin.Abstractions.Attributes
{
    /// <summary>
    /// This attribute is used to identify a renamer.
    /// It is an attribute to allow getting the ID of the renamer without instantiating it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class RenamerIDAttribute : Attribute
    {
        public RenamerIDAttribute(string renamerId)
        {
            RenamerId = renamerId;
        }

        public string RenamerId { get; }
    }
}
