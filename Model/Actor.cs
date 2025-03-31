using System.ComponentModel.DataAnnotations;

namespace Streamflix.Model
{
    public class Actor
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        public string Biography { get; set; }

        public DateTime? BirthDate { get; set; }
    }
}
