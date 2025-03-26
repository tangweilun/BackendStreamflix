using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Streamflix.Model
{
    public class SubscriptionPlan
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string PlanName { get; set; }

        [Required]
        public decimal Price { get; set; }

        [Required]
        public string FeaturesJson { get; set; }

        [Required]
        public string Quality { get; set; }

        [Required]
        public int MaxStreams { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<UserSubscription> UserSubscriptions { get; set; }
    }
}
