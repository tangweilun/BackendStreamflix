using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Streamflix.Model
{
    public class UserSubscription
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int PlanId { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        public bool IsActive { get; set; } = true;

        [ForeignKey("UserId")]
        public User User { get; set; }

        [ForeignKey("PlanId")]
        public SubscriptionPlan SubscriptionPlan { get; set; }
    }
}
