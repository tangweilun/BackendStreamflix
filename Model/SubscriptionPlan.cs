using System.ComponentModel.DataAnnotations;

namespace Streamflix.Model
{
    public class SubscriptionPlan
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string? PlanName { get; set; }

        [Required]
        public double Price { get; set; }

        [Required]
        public string? Quality { get; set; }

        public int MaxStreams { get; set; }

        public bool IsAdmin { get; set; } = true;
    }
}
