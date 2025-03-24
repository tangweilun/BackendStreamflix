using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Streamflix.Model
{
    public class ContentCast
    {
        [Required]
        public int ContentId { get; set; }

        [Required]
        public int ActorId { get; set; }

        [ForeignKey("ContentId")]
        public Content Content { get; set; }

        [ForeignKey("ActorId")]
        public Actor Actor { get; set; }
    }
}
