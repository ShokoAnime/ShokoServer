using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.ScheduledActions;

#nullable enable
namespace Shoko.Server.API.v3.Models.Action;

/// <summary>
/// When a scheduled action runs on its own. Each type sets only the fields
/// whose docs name it; the others are left out or <c>null</c>.
/// </summary>
public class ScheduledActionTrigger
{
    /// <summary>
    /// What makes the action run.
    /// </summary>
    [Required]
    public ActionTriggerType Type { get; set; }

    /// <summary>
    /// How long after the action last ran it runs again, in whole minutes,
    /// from the action's minimum interval to 366 days, with no alignment to
    /// the clock. Set for <see cref="ActionTriggerType.Interval"/> only.
    /// </summary>
    public TimeSpan? Interval { get; set; }

    /// <summary>
    /// The time of day it runs, as <c>HH:mm:ss</c> in the server's time zone,
    /// in whole minutes, so the seconds are <c>00</c>. Set for
    /// <see cref="ActionTriggerType.Daily"/>,
    /// <see cref="ActionTriggerType.Weekly"/> and
    /// <see cref="ActionTriggerType.Monthly"/> only.
    /// </summary>
    public TimeOnly? TimeOfDay { get; set; }

    /// <summary>
    /// The days of the week it runs, at least one, each once. Set for
    /// <see cref="ActionTriggerType.Weekly"/> only.
    /// </summary>
    [JsonProperty(ItemConverterType = typeof(StringEnumConverter))]
    public List<DayOfWeek>? DaysOfWeek { get; set; }

    /// <summary>
    /// The days of the month it runs, at least one, each once: 1 to 31 from
    /// the start of the month, or -1 to -31 from its end, where -1 is the last
    /// day. A day a month does not have is skipped that month. Set for
    /// <see cref="ActionTriggerType.Monthly"/> only.
    /// </summary>
    public List<int>? DaysOfMonth { get; set; }

    /// <summary>
    /// Builds the API form of a trigger.
    /// </summary>
    /// <param name="trigger">The trigger.</param>
    /// <returns>The API form.</returns>
    public static ScheduledActionTrigger FromTrigger(ActionTrigger trigger) => new()
    {
        Type = trigger.Type,
        Interval = trigger.Interval,
        TimeOfDay = trigger.TimeOfDay,
        DaysOfWeek = trigger.DaysOfWeek?.ToList(),
        DaysOfMonth = trigger.DaysOfMonth?.ToList(),
    };

    /// <summary>
    /// Builds the trigger. It still has to be validated.
    /// </summary>
    /// <returns>The trigger.</returns>
    public ActionTrigger ToTrigger() => new()
    {
        Type = Type,
        Interval = Interval,
        TimeOfDay = TimeOfDay,
        DaysOfWeek = DaysOfWeek?.ToList(),
        DaysOfMonth = DaysOfMonth?.ToList(),
    };
}
