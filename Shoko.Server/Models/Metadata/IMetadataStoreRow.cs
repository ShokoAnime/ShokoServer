namespace Shoko.Server.Models.Metadata;

/// <summary>
///   A row in one of the typed metadata stores' tables.
/// </summary>
/// <typeparam name="TSelf">The row's own type.</typeparam>
public interface IMetadataStoreRow<out TSelf> where TSelf : IMetadataStoreRow<TSelf>
{
    /// <summary>
    ///   The row's ID, which is <c>0</c> until it has been saved.
    /// </summary>
    int RowID { get; set; }

    /// <summary>
    ///   A detached copy of the row, to change without touching the cached
    ///   one until the change has been written.
    /// </summary>
    /// <returns>The copy.</returns>
    TSelf Clone();
}
