namespace AniDBAPI.Commands
{
    public abstract class AniDBHTTPCommand
    {
        public string commandID = string.Empty;
        public enAniDBCommandType commandType;
    }
}