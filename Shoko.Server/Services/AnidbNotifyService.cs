using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Events;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Services;

/// <inheritdoc cref="IAnidbNotifyService" />
public class AnidbNotifyService(
    ILogger<AnidbNotifyService> logger,
    ISettingsProvider settingsProvider,
    IQueueScheduler scheduler,
    AniDB_MessageRepository messages
) : IAnidbNotifyService
{
    /// <inheritdoc />
    public event EventHandler<AnidbMessageEventArgs>? MessageReceived;

    /// <inheritdoc />
    public event EventHandler<AnidbMessageEventArgs>? MessageHandled;

    /// <inheritdoc />
    public event EventHandler<AnidbMessageEventArgs>? MessageReadStateChanged;

    /// <inheritdoc />
    public bool IsAutoHandleEnabled
    {
        get => settingsProvider.GetSettings().AniDb.Notification_HandleMovedFiles;
        set
        {
            var settings = settingsProvider.GetSettings();
            if (settings.AniDb.Notification_HandleMovedFiles == value)
                return;

            settings.AniDb.Notification_HandleMovedFiles = value;
            settingsProvider.SaveSettings();
        }
    }

    /// <inheritdoc />
    public bool IsAutoAcknowledgeEnabled
    {
        get => settingsProvider.GetSettings().AniDb.Notification_AcknowledgeOnFetch;
        set
        {
            var settings = settingsProvider.GetSettings();
            if (settings.AniDb.Notification_AcknowledgeOnFetch == value)
                return;

            settings.AniDb.Notification_AcknowledgeOnFetch = value;
            settingsProvider.SaveSettings();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<IAnidbMessage> GetMessages(bool? read = null, bool? handled = null, AnidbMessageKind? kind = null)
        => messages.GetAll()
            .Cast<IAnidbMessage>()
            .Where(message => (read is null || message.IsReadOnShoko == read) && (handled is null || message.IsHandled == handled) && (kind is null || message.Kind == kind))
            .OrderByDescending(message => message.SentAt)
            .ThenByDescending(message => message.ID)
            .ToList();

    /// <inheritdoc />
    public IAnidbMessage? GetMessage(int messageID)
        => messages.GetByMessageId(messageID);

    /// <inheritdoc />
    public async Task HandleMessage(IAnidbMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Kind is not AnidbMessageKind.FileMoved)
            throw new InvalidOperationException($"AniDB message {message.ID} is not one the server can act on.");

        await scheduler.StartJob<ProcessFileMovedMessageJob>(job => job.MessageID = message.ID);
    }

    /// <inheritdoc />
    public IAnidbMessage MarkMessageRead(IAnidbMessage message, bool read = true)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (messages.GetByMessageId(message.ID) is not { } stored)
            throw new ArgumentException($"AniDB message {message.ID} was never fetched.", nameof(message));

        if (stored.IsReadOnShoko == read)
            return stored;

        stored.IsReadOnShoko = read;
        messages.Save(stored);
        RaiseReadStateChanged(stored);
        return stored;
    }

    /// <inheritdoc />
    public async Task AcknowledgeMessage(IAnidbMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.IsReadOnAnidb)
            return;

        await scheduler.StartJob<AcknowledgeAniDBNotifyJob>(job =>
        {
            job.NotifyType = AniDBNotifyType.Message;
            job.NotifyID = message.ID;
        });
    }

    internal void RaiseReceived(AniDB_Message message)
        => Raise(MessageReceived, message, nameof(MessageReceived));

    internal void RaiseHandled(AniDB_Message message)
        => Raise(MessageHandled, message, nameof(MessageHandled));

    internal void RaiseReadStateChanged(AniDB_Message message)
        => Raise(MessageReadStateChanged, message, nameof(MessageReadStateChanged));

    /// <summary>
    ///   Raises an event in the background, so a slow or throwing handler
    ///   never holds up the job or call that raised it.
    /// </summary>
    private void Raise(EventHandler<AnidbMessageEventArgs>? handler, AniDB_Message message, string name)
    {
        if (handler is null)
            return;

        _ = Task.Run(() =>
        {
            try
            {
                handler.Invoke(this, new() { Message = message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "A handler of {Event} threw for AniDB message {MessageID}.", name, message.MessageID);
            }
        });
    }
}
