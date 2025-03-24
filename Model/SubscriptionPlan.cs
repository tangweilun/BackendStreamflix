using System.ComponentModel.DataAnnotations;

namespace Streamflix.Model
{
    public class SubscriptionPlan
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string PlanName { get; set; }

        [Required]
        public decimal Price { get; set; }

        [Required, MaxLength(50)]
        public string Quality { get; set; }

        [Required]
        public int MaxStreams { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<UserSubscription> UserSubscriptions { get; set; }
    }
}
