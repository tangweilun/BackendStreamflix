namespace Streamflix.DTOs
{
    public class WatchProgressUpdateDto
    {
        public int UserId { get; set; }
        public int ContentId { get; set; }
        public int CurrentPosition { get; set; }
    }
}
