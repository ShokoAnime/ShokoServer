using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Events;

namespace Shoko.Abstractions.Metadata.Anidb.Services;

/// <summary>
///   The AniDB user's inbox: the messages the server fetches from AniDB, and
///   acting on, reading and acknowledging them.
/// </summary>
public interface IAnidbNotifyService
{
    /// <summary>
    ///   Dispatched when a message was fetched from AniDB for the first time.
    /// </summary>
    event EventHandler<AnidbMessageEventArgs> MessageReceived;

    /// <summary>
    ///   Dispatched when a message was acted on, by
    ///   <see cref="HandleMessage"/> or automatically.
    /// </summary>
    event EventHandler<AnidbMessageEventArgs> MessageHandled;

    /// <summary>
    ///   Dispatched when a message was marked read or unread on this server,
    ///   or acknowledged to AniDB.
    /// </summary>
    event EventHandler<AnidbMessageEventArgs> MessageReadStateChanged;

    /// <summary>
    ///   Whether messages the server can act on are acted on as soon as they
    ///   arrive (the "Handle Moved Files" setting). Setting it saves the
    ///   server settings.
    /// </summary>
    bool IsAutoHandleEnabled { get; set; }

    /// <summary>
    ///   Whether messages are acknowledged to AniDB as soon as they are
    ///   fetched. Off by default: they stay unread on AniDB until
    ///   <see cref="AcknowledgeMessage"/> is called. Setting it saves the
    ///   server settings.
    /// </summary>
    bool IsAutoAcknowledgeEnabled { get; set; }

    /// <summary>
    ///   Gets the fetched messages, newest first.
    /// </summary>
    /// <param name="read">Only those read on this server, or only those not, or both when <c>null</c>.</param>
    /// <param name="handled">Only those acted on, or only those not, or both when <c>null</c>.</param>
    /// <param name="kind">Only those of one kind, or all when <c>null</c>.</param>
    /// <returns>The messages.</returns>
    IReadOnlyList<IAnidbMessage> GetMessages(bool? read = null, bool? handled = null, AnidbMessageKind? kind = null);

    /// <summary>
    ///   Gets a fetched message by AniDB's ID for it.
    /// </summary>
    /// <param name="messageID">The message's ID.</param>
    /// <returns>The message, or <c>null</c> when none was fetched with that ID.</returns>
    IAnidbMessage? GetMessage(int messageID);

    /// <summary>
    ///   Acts on a message: for <see cref="AnidbMessageKind.FileMoved"/>,
    ///   queues a new release search for the moved file.
    ///   <see cref="MessageHandled"/> is dispatched once the search is done.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <exception cref="InvalidOperationException">The message is of a kind the server has no handling for.</exception>
    /// <returns>A task that completes once the handling is queued.</returns>
    Task HandleMessage(IAnidbMessage message);

    /// <summary>
    ///   Marks a message read or unread on this server. AniDB is not told.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="read">Whether it is read.</param>
    /// <returns>The message, as it is now.</returns>
    IAnidbMessage MarkMessageRead(IAnidbMessage message, bool read = true);

    /// <summary>
    ///   Acknowledges a message to AniDB, which marks it read there. Does
    ///   nothing for one already acknowledged.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>A task that completes once the acknowledgement is queued.</returns>
    Task AcknowledgeMessage(IAnidbMessage message);
}
