using System;
using System.Collections.Generic;
using System.Text;
using Shoko.Server.CommandQueue.Commands;

namespace Shoko.Server.CommandQueue.Preconditions
{
    public class AniDBHttpBan : IPrecondition
    {
        public (bool CanRun, TimeSpan RetryIn) CanExecute()
        {
            if (ShokoService.AnidbProcessor.IsHttpBanned)
                return (false, ShokoService.AnidbProcessor.HttpBanRetryTime);
            return (true, TimeSpan.Zero);
        }
    }
}
