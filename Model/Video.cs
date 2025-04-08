using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Streamflix.Model
{
    public class Video
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Title { get; set; }

        public string Description { get; set; }

        [Required]
        public int Duration { get; set; }

        [Required]
        public string MaturityRating { get; set; }

        [Required]
        public DateTime ReleaseDate { get; set; }

        [Required]
        public string ThumbnailUrl { get; set; }

        [Required]
        public string ContentUrl { get; set; }

        public ICollection<WatchList> WatchLists { get; set; }
        public ICollection<WatchHistory> WatchHistory { get; set; }
        public ICollection<VideoGenre> VideoGenres { get; set; }
        public ICollection<VideoCast> VideoCasts { get; set; }

    }
}