namespace JMMServer.API.Model.core
{
    class QueueInfo
    {
        public int count { get; set; }
        public string state { get; set; }
        public bool isrunning { get; set; }
        public bool ispause { get; set; }
    }
}
