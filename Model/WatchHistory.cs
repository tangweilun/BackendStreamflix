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
        public int ContentId { get; set; }

        public int CurrentPosition { get; set; } = 0;
        
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        [ForeignKey("UserId")]
        public User User { get; set; }

        [ForeignKey("ContentId")]
        public Content Content { get; set; }
    }
}
