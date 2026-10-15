namespace Shoko.Server.Models.Metadata;

/// <summary>
///   A row of a studio or network, which keeps its name on itself rather
///   than in the text store, and may be a stub until its source saves it.
/// </summary>
public interface IMetadataNamedStubRow : IMetadataStubRow
{
    /// <summary>
    ///   The entry's name, or an empty string when a stub was given none.
    /// </summary>
    string Name { get; set; }
}
