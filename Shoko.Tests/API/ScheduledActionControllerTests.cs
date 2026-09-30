using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Abstractions.ScheduledActions.Services;
using Shoko.Server.API.v3.Controllers;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Covers APIv3's scheduled action controller over a mocked service: how
/// trigger edits are built from the triggers in effect, how refusals and
/// unknown scheduled actions come back, and that its routes never collide
/// with the action routes.
/// </summary>
public class ScheduledActionControllerTests
{
    #region Fixture

    private static readonly Guid ActionID = Guid.NewGuid();

    private readonly Mock<IScheduledActionService> _schedule = new();

    private IReadOnlyList<ActionTrigger> _triggers = [ActionTrigger.Every(TimeSpan.FromHours(24))];

    private IReadOnlyList<ActionTrigger>? _stored;

    private TimeSpan _minimum = ActionTrigger.MinimumInterval;

    private static readonly DateTime _lastRunAt = new(2026, 9, 28, 10, 0, 0, DateTimeKind.Utc);

    public ScheduledActionControllerTests()
    {
        _schedule.Setup(schedule => schedule.GetScheduledAction(ActionID)).Returns(() => Info());
        _schedule.Setup(schedule => schedule.SetTriggers(ActionID, It.IsAny<IReadOnlyList<ActionTrigger>>()))
            .Returns<Guid, IReadOnlyList<ActionTrigger>>((_, triggers) =>
            {
                _stored = triggers;
                _triggers = triggers;
                return Info();
            });
        _schedule.Setup(schedule => schedule.ResetTriggers(ActionID))
            .Returns<Guid>(_ =>
            {
                _stored = null;
                _triggers = [ActionTrigger.Every(TimeSpan.FromHours(24))];
                return Info();
            });
    }

    private ScheduledActionInfo Info() => new()
    {
        ID = ActionID,
        Name = "Action",
        Category = ActionCategory.Maintenance,
        CategoryName = "Maintenance",
        PluginID = Guid.Empty,
        Triggers = _triggers,
        DefaultTriggers = [ActionTrigger.Every(TimeSpan.FromHours(24))],
        MinimumInterval = _minimum,
        HasCustomTriggers = _stored is not null,
        ScheduleCountsManualRuns = true,
        LastRunAt = _lastRunAt,
        LastScheduledRunAt = _lastRunAt.AddHours(-2),
        NextRunAt = null,
        State = ScheduledActionState.Idle,
        Progress = null,
        IsCancellable = false,
        JobKey = "Actions/Action",
    };

    private ScheduledActionController Controller() => new(_schedule.Object, new StubSettingsProvider(new ServerSettings()))
    {
        ControllerContext = new() { HttpContext = new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().AddMvcCore().Services.BuildServiceProvider() } },
    };

    #endregion

    #region Triggers

    [Fact]
    public void AddingATrigger_AppendsItToTheOnesInEffect()
    {
        var result = Controller().AddTrigger(ActionID, new() { Type = ActionTriggerType.Daily, TimeOfDay = new(3, 0) });

        Assert.Equal(2, result.Value!.Triggers.Count);
        Assert.Equal([ActionTrigger.Every(TimeSpan.FromHours(24)), ActionTrigger.DailyAt(new TimeOnly(3, 0))], _stored);
    }

    [Fact]
    public void RemovingATrigger_TakesItOutByPosition()
    {
        _triggers = [ActionTrigger.AtStartup, ActionTrigger.Every(TimeSpan.FromHours(1))];

        Controller().RemoveTrigger(ActionID, 0);

        Assert.Equal([ActionTrigger.Every(TimeSpan.FromHours(1))], _stored);
    }

    [Fact]
    public void RemovingATriggerThatIsNotThere_IsNotFound()
    {
        Assert.IsType<NotFoundObjectResult>(Controller().RemoveTrigger(ActionID, 1).Result);
        Assert.IsType<NotFoundObjectResult>(Controller().RemoveTrigger(ActionID, -1).Result);
        _schedule.Verify(schedule => schedule.SetTriggers(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ActionTrigger>>()), Times.Never);
    }

    [Fact]
    public void SettingAnInvalidTrigger_IsAValidationProblem_AndStoresNothing()
    {
        var result = Controller().SetTriggers(ActionID, [new() { Type = ActionTriggerType.Interval, Interval = TimeSpan.FromHours(1) }, new() { Type = ActionTriggerType.Weekly, TimeOfDay = new(3, 0) }]);

        var problem = Assert.IsType<ValidationProblemDetails>(Assert.IsAssignableFrom<ObjectResult>(result.Result).Value);
        Assert.Equal(["[1]"], problem.Errors.Keys);
        _schedule.Verify(schedule => schedule.SetTriggers(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ActionTrigger>>()), Times.Never);
    }

    [Fact]
    public void AnIntervalUnderTheActionsMinimum_IsAValidationProblemNamingIt()
    {
        _minimum = TimeSpan.FromHours(6);

        var result = Controller().AddTrigger(ActionID, new() { Type = ActionTriggerType.Interval, Interval = TimeSpan.FromHours(1) });

        var problem = Assert.IsType<ValidationProblemDetails>(Assert.IsAssignableFrom<ObjectResult>(result.Result).Value);
        Assert.Contains("6 hours", Assert.Single(problem.Errors["[1]"]));
        _schedule.Verify(schedule => schedule.SetTriggers(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ActionTrigger>>()), Times.Never);
    }

    [Fact]
    public void SettingNoTriggers_StoresAnEmptyList_AndResettingStoresNull()
    {
        var controller = Controller();

        Assert.Empty(controller.SetTriggers(ActionID, []).Value!.Triggers);
        Assert.NotNull(_stored);
        Assert.Empty(_stored);

        var reset = controller.ResetTriggers(ActionID).Value!;
        Assert.Null(_stored);
        Assert.False(reset.HasCustomTriggers);
        Assert.Single(reset.Triggers);
    }

    [Fact]
    public void WeeklyAndMonthlyTriggers_AreTakenWithTheirDays_AndShownWithThem()
    {
        var result = Controller().SetTriggers(
            ActionID,
            [
                new() { Type = ActionTriggerType.Weekly, DaysOfWeek = [DayOfWeek.Friday, DayOfWeek.Monday], TimeOfDay = new(2, 0) },
                new() { Type = ActionTriggerType.Monthly, DaysOfMonth = [16, -1], TimeOfDay = new(4, 0) },
            ]
        );

        Assert.Equal([ActionTrigger.WeeklyOn([DayOfWeek.Monday, DayOfWeek.Friday], new TimeOnly(2, 0)), ActionTrigger.MonthlyOn([16, -1], new TimeOnly(4, 0))], _stored);
        var shown = result.Value!.Triggers;
        Assert.Equal([DayOfWeek.Friday, DayOfWeek.Monday], shown[0].DaysOfWeek);
        Assert.Null(shown[0].DaysOfMonth);
        Assert.Equal([16, -1], shown[1].DaysOfMonth);
    }

    [Fact]
    public void TriggersRunningCloserThanTheMinimum_AreAValidationProblem()
    {
        _minimum = TimeSpan.FromHours(6);
        _triggers = [ActionTrigger.DailyAt(new TimeOnly(3, 0))];

        var result = Controller().AddTrigger(ActionID, new() { Type = ActionTriggerType.Daily, TimeOfDay = new(4, 0) });

        var problem = Assert.IsType<ValidationProblemDetails>(Assert.IsAssignableFrom<ObjectResult>(result.Result).Value);
        Assert.Single(problem.Errors[string.Empty]);
        _schedule.Verify(schedule => schedule.SetTriggers(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ActionTrigger>>()), Times.Never);
    }

    [Fact]
    public void AnActionThatCannotBeScheduled_IsNotFound()
    {
        var unknown = Guid.NewGuid();

        Assert.IsType<NotFoundObjectResult>(Controller().GetScheduledAction(unknown).Result);
        Assert.IsType<NotFoundObjectResult>(Controller().SetTriggers(unknown, []).Result);
        Assert.IsType<NotFoundObjectResult>(Controller().ResetTriggers(unknown).Result);
    }

    #endregion

    #region Invoke

    [Fact]
    public async Task RunningAnActionThatRefuses_IsABadRequest()
    {
        _schedule.Setup(schedule => schedule.InvokeAsync(ActionID, It.IsAny<CancellationToken>())).ReturnsAsync(new ActionValidationResult("Not now."));

        var result = await Controller().Invoke(ActionID, TestContext.Current.CancellationToken);

        Assert.Equal("Not now.", Assert.IsType<BadRequestObjectResult>(result.Result).Value);
    }

    #endregion

    #region Triggers In Effect

    [Fact]
    public void TheTriggersInEffect_AreListed()
    {
        _triggers = [ActionTrigger.AtStartup, ActionTrigger.DailyAt(new TimeOnly(4, 0))];

        var triggers = Controller().GetTriggers(ActionID).Value!;

        Assert.Equal([ActionTriggerType.Startup, ActionTriggerType.Daily], triggers.Select(trigger => trigger.Type));
        Assert.IsType<NotFoundObjectResult>(Controller().GetTriggers(Guid.NewGuid()).Result);
    }

    #endregion

    #region Routes

    /// <summary>
    /// The routes of the controllers answering under <c>/api/v3/Action</c>:
    /// the HTTP method, the full template, and the controller and action.
    /// </summary>
    private static List<(string Method, string Template, Type Controller, string Action)> ActionRoutes()
    {
        var routes = new List<(string, string, Type, string)>();
#pragma warning disable CS0618 // The legacy routes answer under the same prefix.
        foreach (var controller in new[] { typeof(ActionController), typeof(LegacyActionController), typeof(ScheduledActionController) })
#pragma warning restore CS0618
        {
            var prefix = controller.GetCustomAttribute<RouteAttribute>()!.Template.Replace("[controller]", "Action");
            foreach (var method in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                foreach (var http in method.GetCustomAttributes<HttpMethodAttribute>())
                {
                    var template = http.Template is { Length: > 0 } suffix ? $"{prefix}/{suffix}" : prefix;
                    foreach (var verb in http.HttpMethods)
                        routes.Add((verb, template, controller, method.Name));
                }
            }
        }

        return routes;
    }

    /// <summary>
    /// Whether a path matches a route template, with its <c>guid</c>, <c>int</c>
    /// and <c>bool</c> constraints, as the router would.
    /// </summary>
    private static bool Matches(string template, string path)
    {
        var parts = template.Trim('/').Split('/');
        var segments = path.Trim('/').Split('/');
        if (parts.Length != segments.Length && !(parts.Length == segments.Length + 1 && parts[^1].EndsWith("?}", StringComparison.Ordinal)))
            return false;

        for (var index = 0; index < segments.Length; index++)
        {
            var part = parts[index];
            var segment = segments[index];
            if (!part.StartsWith('{'))
            {
                if (!string.Equals(part, segment, StringComparison.OrdinalIgnoreCase))
                    return false;
                continue;
            }

            var constraint = part.Trim('{', '}', '?').Split(':').Skip(1).FirstOrDefault();
            var valid = constraint switch
            {
                "guid" => Guid.TryParse(segment, out _),
                "int" => int.TryParse(segment, out _),
                "bool" => bool.TryParse(segment, out _),
                _ => true,
            };
            if (!valid)
                return false;
        }

        return true;
    }

    [Fact]
    public void NoActionRoute_TakesTheScheduledSegment()
    {
        var clashing = ActionRoutes()
            .Where(route => route.Controller != typeof(ScheduledActionController))
            .Where(route => Matches(route.Template.Replace("v{version:apiVersion}", "v3"), "/api/v3/Action/Scheduled"))
            .ToList();

        Assert.Empty(clashing);
    }

    #endregion
}
