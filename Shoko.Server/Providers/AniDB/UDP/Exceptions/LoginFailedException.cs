using System;
using Shoko.Server.Services.ErrorHandling;

namespace Shoko.Server.Providers.AniDB.UDP.Exceptions;

[SentryIgnore]
public class LoginFailedException : Exception
{
}
