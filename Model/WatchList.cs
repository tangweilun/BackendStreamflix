using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Streamflix.Model
{
    public class WatchList
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int ContentId { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("UserId")]
        public User User { get; set; }

        [ForeignKey("ContentId")]
        public Video Content { get; set; }
    }
}
