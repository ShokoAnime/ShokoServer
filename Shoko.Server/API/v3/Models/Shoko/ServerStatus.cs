using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko;

public class ServerStatus
{
    /// <summary>
    /// The state of startup.
    /// </summary>
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public StartupState State { get; set; }

    /// <summary>
    /// The progress message for starting up
    /// </summary>
    public string? StartupMessage { get; set; }

    /// <summary>
    /// Indicates that we can perform a controlled shutdown.
    /// </summary>
    /// <remarks>
    /// Only set for authenticated requests.
    /// </remarks>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public bool? CanShutdown { get; set; }

    /// <summary>
    /// Indicates that we can perform a controlled restart.
    /// </summary>
    /// <remarks>
    /// Only set for authenticated requests.
    /// </remarks>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public bool? CanRestart { get; set; }

    /// <summary>
    /// The time the server started bootstrapping.
    /// </summary>
    /// <remarks>
    /// Only set for authenticated requests.
    /// </remarks>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public DateTime? BootstrappedAt { get; set; }

    /// <summary>
    /// The time the server was fully started after the initial bootstrapping.
    /// </summary>
    /// <remarks>
    /// Only set for authenticated requests.
    /// </remarks>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Uptime since bootstrapping took place. Uses hours may be greater than a day.
    /// </summary>
    /// <remarks>
    /// Only set for authenticated requests.
    /// </remarks>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public TimeSpan? Uptime { get; set; }

    /// <summary>
    /// The time it took to start up. Will be zero if not started.
    /// </summary>
    /// <remarks>
    /// Only set for authenticated requests.
    /// </remarks>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public TimeSpan? StartupTime { get; set; }
    /// <summary>
    /// This is true in situations where there can be absolutely no write operations.
    /// This is for polling. Ideally, a client will use the Events SignalR Hub.
    /// </summary>
    /// <remarks>
    /// Only set for authenticated requests.
    /// </remarks>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public DatabaseBlockedInfo? DatabaseBlocked { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum StartupState
    {
        /// <summary>
        /// Starting up
        /// </summary>
        Starting = 1,

        /// <summary>
        /// Finished starting
        /// </summary>
        Started = 2,

        /// <summary>
        /// There was an error while starting
        /// </summary>
        Failed = 3,

        /// <summary>
        /// Waiting for setup
        /// </summary>
        Waiting = 4,
    }

    public class DatabaseBlockedInfo
    {
        /// <summary>
        /// Whether the system is blocked or not
        /// </summary>
        public required bool Blocked { get; init; }

        /// <summary>
        /// A message about the blocked state
        /// </summary>
        public required string? Reason { get; set; }
    }
}
