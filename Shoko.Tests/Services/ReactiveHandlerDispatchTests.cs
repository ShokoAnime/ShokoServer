using Namotion.Reflection;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.Server.Services.Configuration;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   Coverage for how <see cref="ConfigurationService"/> picks the handler for a
///   lifecycle hook.
/// </summary>
/// <remarks>
///   Every lookup used to ask for a live-edit handler whatever hook was being
///   run, so a configuration declaring a <c>Load</c> handler either ran its
///   live-edit handler instead or resolved nothing at all — and
///   <c>GET /Configuration/{configID}</c>, which only calls the hook once
///   <c>HasCustomLoad</c> says there is one, answered 409.
/// </remarks>
public class ReactiveHandlerDispatchTests
{
    [Theory]
    [InlineData(ConfigurationActionType.Load, nameof(EveryHookConfiguration.OnLoad))]
    [InlineData(ConfigurationActionType.New, nameof(EveryHookConfiguration.OnNew))]
    [InlineData(ConfigurationActionType.Save, nameof(EveryHookConfiguration.OnSave))]
    [InlineData(ConfigurationActionType.Validate, nameof(EveryHookConfiguration.OnValidate))]
    [InlineData(ConfigurationActionType.LiveEdit, nameof(EveryHookConfiguration.OnEdit))]
    public void EachHook_ResolvesItsOwnHandler(ConfigurationActionType actionType, string expected)
    {
        var method = ConfigurationService.FindReactiveHandler(typeof(EveryHookConfiguration).ToContextualType(), actionType, ReactiveEventType.All);

        Assert.Equal(expected, method?.Name);
    }

    [Fact]
    public void AHookWithNoHandler_ResolvesNothing()
    {
        // The class declares a live edit handler and nothing else, which is the
        // shape that used to answer every hook with `OnEdit`.
        var type = typeof(LiveEditOnlyConfiguration).ToContextualType();

        Assert.Null(ConfigurationService.FindReactiveHandler(type, ConfigurationActionType.Load, ReactiveEventType.All));
        Assert.Null(ConfigurationService.FindReactiveHandler(type, ConfigurationActionType.New, ReactiveEventType.All));
        Assert.Null(ConfigurationService.FindReactiveHandler(type, ConfigurationActionType.Save, ReactiveEventType.All));
        Assert.Null(ConfigurationService.FindReactiveHandler(type, ConfigurationActionType.Validate, ReactiveEventType.All));
        Assert.Equal(nameof(LiveEditOnlyConfiguration.OnEdit), ConfigurationService.FindReactiveHandler(type, ConfigurationActionType.LiveEdit, ReactiveEventType.All)?.Name);
    }

    [Theory]
    [InlineData(ReactiveEventType.Unfocused, nameof(EventfulConfiguration.OnUnfocused))]
    [InlineData(ReactiveEventType.NewValue, nameof(EventfulConfiguration.OnNewValue))]
    // No handler declares these, so the one that took everything stands in.
    [InlineData(ReactiveEventType.Edited, nameof(EventfulConfiguration.OnAnything))]
    [InlineData(ReactiveEventType.Clicked, nameof(EventfulConfiguration.OnAnything))]
    [InlineData(ReactiveEventType.All, nameof(EventfulConfiguration.OnAnything))]
    public void LiveEdit_PicksTheHandlerForTheEvent(ReactiveEventType reactiveEventType, string expected)
    {
        var method = ConfigurationService.FindReactiveHandler(typeof(EventfulConfiguration).ToContextualType(), ConfigurationActionType.LiveEdit, reactiveEventType);

        Assert.Equal(expected, method?.Name);
    }

    [Fact]
    public void LiveEditForAnEvent_DoesNotFallBackToAnotherEventsHandler()
    {
        // `All` is the only stand-in. A handler that named one event is never
        // handed another one.
        var type = typeof(UnfocusedOnlyConfiguration).ToContextualType();

        Assert.Equal(nameof(UnfocusedOnlyConfiguration.OnUnfocused), ConfigurationService.FindReactiveHandler(type, ConfigurationActionType.LiveEdit, ReactiveEventType.Unfocused)?.Name);
        Assert.Null(ConfigurationService.FindReactiveHandler(type, ConfigurationActionType.LiveEdit, ReactiveEventType.Edited));
        Assert.Null(ConfigurationService.FindReactiveHandler(type, ConfigurationActionType.LiveEdit, ReactiveEventType.All));
    }

    /// <summary>A configuration declaring one handler per hook.</summary>
    public class EveryHookConfiguration : IConfiguration
    {
        /// <summary>Runs on load.</summary>
        [ConfigurationAction(ConfigurationActionType.Load)]
        public void OnLoad() { }

        /// <summary>Runs on new.</summary>
        [ConfigurationAction(ConfigurationActionType.New)]
        public void OnNew() { }

        /// <summary>Runs on save.</summary>
        [ConfigurationAction(ConfigurationActionType.Save)]
        public void OnSave() { }

        /// <summary>Runs on validate.</summary>
        [ConfigurationAction(ConfigurationActionType.Validate)]
        public void OnValidate() { }

        /// <summary>Runs while editing.</summary>
        [ConfigurationAction(ConfigurationActionType.LiveEdit)]
        public void OnEdit() { }
    }

    /// <summary>A configuration declaring nothing but a live edit handler.</summary>
    public class LiveEditOnlyConfiguration : IConfiguration
    {
        /// <summary>Runs while editing.</summary>
        [ConfigurationAction(ConfigurationActionType.LiveEdit)]
        public void OnEdit() { }
    }

    /// <summary>A configuration declaring live edit handlers per event.</summary>
    public class EventfulConfiguration : IConfiguration
    {
        /// <summary>Runs when a field is unfocused.</summary>
        [ConfigurationAction(ConfigurationActionType.LiveEdit, ReactiveEventType = ReactiveEventType.Unfocused)]
        public void OnUnfocused() { }

        /// <summary>Runs when a new value is being added.</summary>
        [ConfigurationAction(ConfigurationActionType.LiveEdit, ReactiveEventType = ReactiveEventType.NewValue)]
        public void OnNewValue() { }

        /// <summary>Runs for everything else.</summary>
        [ConfigurationAction(ConfigurationActionType.LiveEdit)]
        public void OnAnything() { }
    }

    /// <summary>A configuration whose only handler named one event.</summary>
    public class UnfocusedOnlyConfiguration : IConfiguration
    {
        /// <summary>Runs when a field is unfocused.</summary>
        [ConfigurationAction(ConfigurationActionType.LiveEdit, ReactiveEventType = ReactiveEventType.Unfocused)]
        public void OnUnfocused() { }
    }
}
