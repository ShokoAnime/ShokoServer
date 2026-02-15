using System;
using Shoko.Server.Services.ErrorHandling;

namespace Shoko.Server.Exceptions;

[SentryInclude]
public class InvalidStateException : Exception
{
    public InvalidStateException(string message) : base(message) { }
}
