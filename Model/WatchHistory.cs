using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Streamflix.Model
{
    public class WatchHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int VideoId { get; set; }

        public int CurrentPosition { get; set; }
        
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        [ForeignKey("UserId")]
        public User User { get; set; }

        [ForeignKey("VideoId")]
        public Video Video { get; set; }
    }
}
