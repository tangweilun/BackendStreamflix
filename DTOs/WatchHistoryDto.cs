namespace Streamflix.DTOs
{
    public class WatchHistoryDto
    {
        public int UserId { get; set; }
        public string VideoTitle { get; set; }
        public int CurrentPosition { get; set; }
    }
}
