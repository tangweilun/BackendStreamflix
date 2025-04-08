using System.ComponentModel.DataAnnotations;

namespace Streamflix.Model
{
    public class Genre
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string GenreName { get; set; }

        public ICollection<VideoGenre> VideoGenres { get; set; }
    }
}