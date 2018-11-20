using System;
using System.Collections.Generic;
using System.Text;
using Shoko.Server.CommandQueue.Commands;
using Shoko.Server.Providers.AniDB;

namespace Shoko.Server.CommandQueue.Preconditions
{
    public class AniDBUDPBan : IPrecondition
    {
        public (bool CanRun, TimeSpan RetryIn) CanExecute()
        {
            if (ShokoService.AnidbProcessor.IsUdpBanned)
                return (false, ShokoService.AnidbProcessor.UdpBanRetryTime);
            return (true, TimeSpan.Zero);
        }
    }
}
