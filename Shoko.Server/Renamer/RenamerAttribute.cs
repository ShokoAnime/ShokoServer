using System;

namespace Shoko.Server.Renamer
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class RenamerAttribute : Attribute
    {
        public RenamerAttribute(string renamerId)
        {
            RenamerId = renamerId;
        }

        public string RenamerId { get; }
    }
}