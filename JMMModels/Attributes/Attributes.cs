using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMModels.Attributes
{

    public class Level : Attribute //Attributes for transversal cut of the model serialization
    {
        public int TillLevel { get; set; } = int.MaxValue;
        public bool InheritedLevel { get; set; } = false;
        internal int Lvl { get; private set; }

        public Level(int level)
        {
            Lvl = level;
        }
    }
}
