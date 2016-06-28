using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Configuration;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.WCFCompression
{
    public class MultiBehaviorExtension : BehaviorExtensionElement
    {
        public override Type BehaviorType
        {
            get { return typeof(MultiBehavior); }
        }

        protected override object CreateBehavior()
        {
            return new MultiBehavior();
        }
    }

}
