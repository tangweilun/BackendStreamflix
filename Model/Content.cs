using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Streamflix.Model
{
    public class Content
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(255)]
        public string Title { get; set; }

        public string Description { get; set; }

        [Required, MaxLength(50)]
        public string Type { get; set; }

        [Required]
        public int Duration { get; set; }

        [Required, MaxLength(10)]
        public string MaturityRating { get; set; }

        [Required]
        public DateTime ReleaseDate { get; set; }

        [Required, MaxLength(255)]
        public string ThumbnailUrl { get; set; }

        [Required, MaxLength(255)]
        public string ContentUrl { get; set; }

        public ICollection<WatchList> WatchLists { get; set; }
        public ICollection<ContentGenre> ContentGenres { get; set; }
        public ICollection<ContentCast> ContentCasts { get; set; }
    }
}
