using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Streamflix.Model
{
    public class VideoGenre
    {
        [Required]
        public int VideoId { get; set; }

        [Required]
        public int GenreId { get; set; }

        [ForeignKey("VideoId")]
        public Video Video { get; set; }

        [ForeignKey("GenreId")]
        public Genre Genre { get; set; }
    }
}
