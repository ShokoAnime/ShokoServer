using System;
using System.Collections.Generic;
using System.Text;

namespace Shoko.Plugin.Abstractions
{
    public interface IRenameScript
    {
        /// <summary>
        /// A unique name for this particular implementation, per Type
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// The script contents
        /// </summary>
        public string Script { get; }
        /// <summary>
        /// The type of the renamer, always should be checked against the Renamer ID to ensure that the script should be executable against your renamer.
        /// </summary>
        public string Type { get; }
        /// <summary>
        /// Any extra data provided.
        /// </summary>
        public string ExtraData { get; }
    }
}
