using System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Filtering.Events;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Server.API.SignalR.Models;

namespace Shoko.Server.API.SignalR.Aggregate;

/// <summary>
/// The <c>filter</c> feed: filter presets being stored, updated and deleted,
/// as <c>filter:preset.stored</c>, <c>filter:preset.updated</c> and
/// <c>filter:preset.deleted</c>.
/// </summary>
public class FilterEventEmitter : BaseEventEmitter, IDisposable
{
    public override string Name => "filter";

    private readonly IFilterPresetManager _presetManager;

    private readonly ILogger<FilterEventEmitter> _logger;

    public FilterEventEmitter(IHubContext<AggregateHub> hub, IFilterPresetManager presetManager, ILogger<FilterEventEmitter> logger) : base(hub)
    {
        _presetManager = presetManager;
        _logger = logger;
        _presetManager.PresetStored += OnPresetStored;
        _presetManager.PresetUpdated += OnPresetUpdated;
        _presetManager.PresetDeleted += OnPresetDeleted;
    }

    public void Dispose()
    {
        _presetManager.PresetStored -= OnPresetStored;
        _presetManager.PresetUpdated -= OnPresetUpdated;
        _presetManager.PresetDeleted -= OnPresetDeleted;
    }

    private void OnPresetStored(object? sender, FilterPresetEventArgs e)
        => Send("preset.stored", e);

    private void OnPresetUpdated(object? sender, FilterPresetEventArgs e)
        => Send("preset.updated", e);

    private void OnPresetDeleted(object? sender, FilterPresetEventArgs e)
        => Send("preset.deleted", e);

    private async void Send(string subject, FilterPresetEventArgs e)
    {
        try
        {
            await SendAsync(subject, new FilterPresetSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the '{Subject}' event.", subject);
        }
    }
}
