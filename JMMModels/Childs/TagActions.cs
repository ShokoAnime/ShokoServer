using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMModels.Childs
{
    public enum TagActions
    {
        None=0,
        CheckAniDBForBluRay=1,
        BluRayComplete=2,
    }
    [Flags]
    public enum TagActionsTriggers
    {
        None = 0,
        Schedule=1,
        ModifyCreate=2
    }
}
