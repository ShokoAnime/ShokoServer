using System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.User;
using Shoko.Abstractions.Video.Events;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.API.SignalR.Models;

namespace Shoko.Server.API.SignalR.Aggregate;

/// <summary>
/// The <c>relocation</c> feed, for admins only: relocation presets being
/// stored, updated and deleted, as <c>relocation:preset.stored</c>,
/// <c>relocation:preset.updated</c> and <c>relocation:preset.deleted</c>.
/// </summary>
public class RelocationEventEmitter : BaseEventEmitter, IDisposable
{
    public override string Name => "relocation";

    private readonly IRelocationPresetManager _presetManager;

    private readonly ILogger<RelocationEventEmitter> _logger;

    public RelocationEventEmitter(IHubContext<AggregateHub> hub, IRelocationPresetManager presetManager, ILogger<RelocationEventEmitter> logger) : base(hub)
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

    /// <inheritdoc/>
    protected override bool CanConnect(IUser user)
        => user.IsAdmin;

    private void OnPresetStored(object? sender, RelocationPresetEventArgs e)
        => Send("preset.stored", e);

    private void OnPresetUpdated(object? sender, RelocationPresetEventArgs e)
        => Send("preset.updated", e);

    private void OnPresetDeleted(object? sender, RelocationPresetEventArgs e)
        => Send("preset.deleted", e);

    private async void Send(string subject, RelocationPresetEventArgs e)
    {
        try
        {
            await SendAsync(subject, new RelocationPresetSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the '{Subject}' event.", subject);
        }
    }
}
