using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Streamflix.Model
{
    public class ContentGenre
    {
        [Required]
        public int ContentId { get; set; }

        [Required]
        public int GenreId { get; set; }

        [ForeignKey("ContentId")]
        public Content Content { get; set; }

        [ForeignKey("GenreId")]
        public Genre Genre { get; set; }
    }
}
