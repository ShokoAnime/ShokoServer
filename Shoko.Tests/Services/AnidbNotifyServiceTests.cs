using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Server;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   The AniDB inbox as plugins see it: what a message is about, whether it
///   was acted on, and which messages the server can act on.
/// </summary>
public class AnidbNotifyServiceTests
{
    private static AniDB_Message Message(int id, string title, AniDBMessageFlags flags = AniDBMessageFlags.None, AniDBMessageType type = AniDBMessageType.System)
        => new()
        {
            MessageID = id,
            Title = title,
            Type = type,
            Flags = flags,
            SentAt = DateTime.UnixEpoch.AddDays(id),
        };

    [Fact]
    public void AFileMovedMessageNamesItsFile()
    {
        IAnidbMessage message = Message(1, "file moved: 3464979", AniDBMessageFlags.FileMoved);

        Assert.Equal(AnidbMessageKind.FileMoved, message.Kind);
        Assert.Equal(3464979, message.MovedFileID);
        Assert.Equal(AnidbMessageType.System, message.Type);
    }

    [Fact]
    public void APlainMessageIsNeverHandled()
    {
        IAnidbMessage message = Message(2, "[BUDDY REQUEST ACCEPTED] someone", AniDBMessageFlags.FileMoveHandled);

        Assert.Equal(AnidbMessageKind.Generic, message.Kind);
        Assert.Null(message.MovedFileID);
        Assert.False(message.IsHandled);
    }

    [Fact]
    public void HandledAutomaticallyIsKeptApart()
    {
        IAnidbMessage byHand = Message(3, "file moved: 1", AniDBMessageFlags.FileMoved | AniDBMessageFlags.FileMoveHandled);
        IAnidbMessage automatic = Message(4, "file moved: 2", AniDBMessageFlags.FileMoved | AniDBMessageFlags.FileMoveHandled | AniDBMessageFlags.FileMoveHandledAutomatically);

        Assert.True(byHand.IsHandled);
        Assert.False(byHand.IsHandledAutomatically);
        Assert.True(automatic.IsHandledAutomatically);
    }

    [Fact]
    public void MessagesAreFilteredAndNewestFirst()
    {
        var service = Service(
            Message(1, "file moved: 1", AniDBMessageFlags.FileMoved | AniDBMessageFlags.FileMoveHandled),
            Message(2, "file moved: 2", AniDBMessageFlags.FileMoved),
            Message(3, "hello", AniDBMessageFlags.ReadOnShoko, AniDBMessageType.Normal)
        );

        Assert.Equal([3, 2, 1], service.GetMessages().Select(message => message.ID));
        Assert.Equal([2], service.GetMessages(handled: false, kind: AnidbMessageKind.FileMoved).Select(message => message.ID));
        Assert.Equal([3], service.GetMessages(read: true).Select(message => message.ID));
    }

    [Fact]
    public async Task OnlyAFileMovedMessageCanBeHandled()
        => await Assert.ThrowsAsync<InvalidOperationException>(() => Service().HandleMessage(Message(5, "hello", type: AniDBMessageType.Normal)));

    [Fact]
    public void TheAutoSettingsSaveOnlyWhenChanged()
    {
        var settings = new ServerSettings();
        var provider = new Mock<ISettingsProvider>();
        provider.Setup(p => p.GetSettings(It.IsAny<bool>())).Returns(settings);
        var service = new AnidbNotifyService(NullLogger<AnidbNotifyService>.Instance, provider.Object, Mock.Of<IQueueScheduler>(), null!);

        Assert.False(service.IsAutoAcknowledgeEnabled);
        service.IsAutoAcknowledgeEnabled = true;
        service.IsAutoAcknowledgeEnabled = true;
        service.IsAutoHandleEnabled = true;

        Assert.True(settings.AniDb.Notification_AcknowledgeOnFetch);
        Assert.True(settings.AniDb.Notification_HandleMovedFiles);
        provider.Verify(p => p.SaveSettings(), Times.Exactly(2));
    }

    private static AnidbNotifyService Service(params AniDB_Message[] messages)
    {
        var repository = new Mock<AniDB_MessageRepository>(new object[] { null! });
        repository.Setup(repo => repo.GetAll()).Returns(messages.ToList());
        return new(NullLogger<AnidbNotifyService>.Instance, Mock.Of<ISettingsProvider>(), Mock.Of<IQueueScheduler>(), repository.Object);
    }
}
