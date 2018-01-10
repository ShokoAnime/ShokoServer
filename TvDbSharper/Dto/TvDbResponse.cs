namespace TvDbSharper.Dto
{
    public class TvDbResponse<TData>
    {
        public TData Data { get; set; }

        public Errors Errors { get; set; }

        public Links Links { get; set; }
    }
}