using Shoko.Server.Providers.AniDB.MyList.Exceptions;

namespace Shoko.Server.Providers.AniDB.MyList.Commands
{
    public abstract class AniDBUDP_BaseRequest<T> where T : class
    {
        /// <summary>
        /// Various Parameters to add to the base command
        /// </summary>
        protected abstract string Command { get; }

        protected bool HasEexecuted { get; set; }
        
        protected AniDBUDP_Response<T> _response { get; set; }
        
        /// <summary>
        /// The Response
        /// </summary>
        protected AniDBUDP_Response<T> Response {
            get
            {
                if (!HasEexecuted) throw new CommandNotExecutedException();;
                return _response;
            }
            set => _response = value;
        }

        protected abstract T ParseResponse(AniDBUDPReturnCode code, string receivedData);

        public void Execute()
        {
            // TODO Adapt AniDBHelper to be less interdependent on the previous system

            
            HasEexecuted = true;
        }

        protected void PreExecute()
        {
            
        }
    }
}