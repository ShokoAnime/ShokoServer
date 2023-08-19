using System;
using Shoko.Server.Models;

namespace Shoko.Server.Commands;

public interface ICommandRequestFactory
{
    T Create<T>(Action<T> ctor = null) where T : CommandRequest;
    void CreateAndSave<T>(Action<T> ctor = null, bool force = false) where T : CommandRequest;
    void Save(CommandRequest request, bool force = false);
}
