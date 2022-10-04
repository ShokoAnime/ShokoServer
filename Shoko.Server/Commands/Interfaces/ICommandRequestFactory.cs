using System;
using Shoko.Server.Commands.Interfaces;

namespace Shoko.Server.Commands;

public interface ICommandRequestFactory
{
    T Create<T>(Action<T> ctor = null) where T : ICommandRequest;
}
