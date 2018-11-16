using System;
using Newtonsoft.Json;

namespace Shoko.Server.CommandQueue.Commands
{
    public interface IPrecondition
    {
        (bool CanRun, TimeSpan RetryIn) CanExecute();

    }
}