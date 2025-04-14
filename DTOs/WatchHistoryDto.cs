namespace Streamflix.DTOs
{
    public class WatchHistoryDto
    {
        public int UserId { get; set; }
        public int VideoId { get; set; }
        public int? CurrentPosition { get; set; }
    }
}
