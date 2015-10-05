using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMModels.Childs;

namespace JMMModels
{
    public class RecomendationIgnore
    {
        public string JMMUserId { get; set; }
        public RecomedationType Ignore { get; set; }
    }
}
