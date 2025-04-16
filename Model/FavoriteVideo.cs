
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Streamflix.Model
{
    public class FavoriteVideo
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public string VideoTitle { get; set; }

        [Required]
        public DateTime DateAdded { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; }

        public Video Video { get; set; } // ForeignKey attribute removed here
    }

}
