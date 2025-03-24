using System.ComponentModel.DataAnnotations;

namespace Streamflix.Model
{
    public class Genre
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string GenreName { get; set; }

        public string Description { get; set; }
    }
}
