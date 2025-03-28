using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Stripe;

namespace Streamflix.Model
{
    public enum SubscriptionStatus
    {
        Pending,
        Ongoing,
        Expired
    }

    public class UserSubscription
    {
        [Key]
        public int Id { get; set; }

        // Store subscription ID from Stripe for future reference
        [Required]
        public string StripeSubscriptionId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int PlanId { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        public SubscriptionStatus Status { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; }

        [ForeignKey("PlanId")]
        public SubscriptionPlan SubscriptionPlan { get; set; }
    }
}
