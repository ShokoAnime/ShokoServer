//from https://github.com/mj1856/EntityFramework.IndexingExtensions
// ReSharper disable CheckNamespace

namespace System.Data.Entity
{
    /// <summary>
    /// Specifies options for an index.
    /// </summary>
    [Flags]
    public enum IndexOptions
    {
        /// <summary>
        /// A non-clustered index.
        /// </summary>
        Nonclustered = 0,

        /// <summary>
        /// A clustered index.
        /// </summary>
        Clustered = 1,

        /// <summary>
        /// A unique index.
        /// </summary>
        Unique = 2,

        /// <summary>
        /// A clustered, unique index.
        /// </summary>
        ClusteredUnique = Clustered | Unique
    }
}
