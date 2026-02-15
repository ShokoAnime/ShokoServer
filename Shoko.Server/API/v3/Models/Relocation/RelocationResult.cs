using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Shoko.Server.API.v3.Models.Shoko;

#nullable enable
namespace Shoko.Server.API.v3.Models.Relocation;

/// <summary>
/// Represents the result of a file relocation process.
/// </summary>
public class RelocationResult
{
    /// <summary>
    /// The file id.
    /// </summary>
    [Required]
    public int FileID { get; set; }

    /// <summary>
    /// The file location id. May be null if a location to use was not
    /// found.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? FileLocationID { get; set; } = null;

    /// <summary>
    /// The name of the config that produced the final location for the
    /// file if the relocation was successful and was not the result of
    /// a manual relocation.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? PipeName { get; set; } = null;

    /// <summary>
    /// The new id of the <see cref="ManagedFolder"/> where the file now
    /// resides, if the relocation was successful. Remember to check
    /// <see cref="IsSuccess"/> to see the status of the relocation.
    /// </summary>
    /// 
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? ManagedFolderID { get; set; } = null;

    /// <summary>
    /// Indicates whether the file was relocated successfully.
    /// </summary>
    [Required]
    public bool IsSuccess { get; set; } = false;

    /// <summary>
    /// Indicates whether the file was actually relocated from one
    /// location to another, or if it was already at it's correct
    /// location.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsRelocated { get; set; } = null;

    /// <summary>
    /// Indicates if the result is only a preview and the file has not
    /// actually been relocated yet.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsPreview { get; set; } = null;

    /// <summary>
    /// The error message if the relocation was not successful.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? ErrorMessage { get; set; } = null;

    /// <summary>
    /// The new relative path from the <see cref="ManagedFolder"/>'s path
    /// on the server, if relocation was successful. Remember to check
    /// <see cref="IsSuccess"/> to see the status of the relocation.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? RelativePath { get; set; } = null;

    /// <summary>
    /// The new absolute path for the file on the server, if relocation
    /// was successful. Remember to check <see cref="IsSuccess"/> to see
    /// the status of the relocation.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? AbsolutePath { get; set; } = null;
}
