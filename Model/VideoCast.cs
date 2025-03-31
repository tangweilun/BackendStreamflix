using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Streamflix.Model
{
    public class VideoCast
    {
        [Required]
        public int VideoId { get; set; }

        [Required]
        public int ActorId { get; set; }

        [ForeignKey("VideoId")]
        public Video Video { get; set; }

        [ForeignKey("ActorId")]
        public Actor Actor { get; set; }
    }
}
