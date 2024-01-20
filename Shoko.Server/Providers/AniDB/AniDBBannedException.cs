using System;
using Shoko.Server.Services.ErrorHandling;

namespace Shoko.Server.Providers.AniDB;

[Serializable, SentryIgnore]
public class AniDBBannedException : Exception
{
    public UpdateType BanType { get; set; }
    public DateTime? BanExpires { get; set; }
}
