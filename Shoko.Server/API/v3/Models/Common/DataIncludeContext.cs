namespace Shoko.Server.API.v3.Models.Common;

public class DataIncludeContext
{
    /// <summary>
    /// Include ignored files among the results
    /// </summary>
    public IncludeOnlyFilter IncludeIgnored { get; set; } = IncludeOnlyFilter.False;
    /// <summary>
    /// Include files marked as a variation among the results
    /// </summary>
    public IncludeOnlyFilter IncludeVariations { get; set; } = IncludeOnlyFilter.True;
    /// <summary>
    /// Include files with multiple locations (and thus have duplicates) among the results
    /// </summary>
    public IncludeOnlyFilter IncludeDuplicates { get; set; } = IncludeOnlyFilter.True;
    /// <summary>
    /// Include unrecognized files among the results
    /// </summary>
    public IncludeOnlyFilter IncludeUnrecognized { get; set; } = IncludeOnlyFilter.True;
    /// <summary>
    /// Include manually linked files among the results
    /// </summary>
    public IncludeOnlyFilter IncludeManuallyLinked { get; set; } = IncludeOnlyFilter.True;
    /// <summary>
    /// Include previously viewed files among the results
    /// </summary>
    public IncludeOnlyFilter IncludeViewed { get; set; } = IncludeOnlyFilter.True;
    /// <summary>
    /// Include previously watched files among the results
    /// </summary>
    public IncludeOnlyFilter IncludeWatched { get; set; } = IncludeOnlyFilter.True;
    /// <summary>
    /// Include media info data
    /// </summary>
    public IncludeOnlyFilter IncludeMediaInfo { get; set; } = IncludeOnlyFilter.False;
    /// <summary>
    /// Include absolute paths for the file locations
    /// </summary>
    public IncludeOnlyFilter IncludeAbsolutePaths { get; set; } = IncludeOnlyFilter.False;
    /// <summary>
    /// Include series and episode cross-references
    /// </summary>
    public IncludeOnlyFilter IncludeXRefs { get; set; } = IncludeOnlyFilter.False;
}
