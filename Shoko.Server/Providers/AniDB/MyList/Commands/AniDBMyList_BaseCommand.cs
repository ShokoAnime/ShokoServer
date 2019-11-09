namespace Shoko.Server.Providers.AniDB.MyList.Commands
{
    public abstract class AniDBMyList_BaseCommand<T> where T : class
    {
        /// <summary>
        /// The Base Command without parameters
        /// </summary>
        protected abstract string BaseCommand { get; set; }
        
        /// <summary>
        /// Various Parameters to add to the base command
        /// </summary>
        protected string Command { get; set; }

        /// <summary>
        /// The Response
        /// </summary>
        protected AniDBMyList_Response<T> Response { get; set; }

        public virtual void Execute()
        {
            
        }
    }
}