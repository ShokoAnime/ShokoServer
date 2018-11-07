using System;
using System.Collections.Generic;
using System.Text;

namespace Shoko.Server.CommandQueue.Commands
{
    public class ChildProgress : IProgress<double> 
    {
        private readonly int _max;
        private readonly IProgress<ICommand> _orgp;
        private readonly BaseCommand _original;
        private readonly int _start;

        public ChildProgress(int start, int max, BaseCommand original, IProgress<ICommand> pro)
        {
            _start = start;
            _max = max;
            _original = original;
            _orgp = pro;
        }

        public void Report(double value)
        {
            _original.UpdateAndReportProgress(_orgp, _start + value * _max / 100D);
        }
    }
}
