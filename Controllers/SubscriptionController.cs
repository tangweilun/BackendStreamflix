using Humanizer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Streamflix.Data;
using Streamflix.DTOs;
using Streamflix.Model;
using Stripe;
using Stripe.Checkout;

namespace Streamflix.Controllers
{
    [ApiController]
    [Route("api/subscription")]
    public class SubscriptionController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly StripeSettings _stripeSettings;
        //private readonly CustomerService _customerService;

        public SubscriptionController(ApplicationDbContext context, IOptions<StripeSettings> stripeSettings)
        {
            _context = context;
            _stripeSettings = stripeSettings.Value;
            //_customerService = customerService;
        }

        [HttpGet("get-all-plans")]
        public async Task<IActionResult> GetAllPlans()
        {
            var plans = await _context.SubscriptionPlans
                .Where(plan => plan.IsActive)
                .OrderBy(plan => plan.Id)
                .ToListAsync();

            return Ok(plans);
        }

        [HttpPost("subscribe")]
        public async Task<IActionResult> Pay([FromBody] UserSubscriptionDto subscriptionDto)
        {
            StripeConfiguration.ApiKey = _stripeSettings.SecretKey;

            var plan = await _context.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == subscriptionDto.PlanId);

            if (plan == null)
            {
                return NotFound("Subscription plan not found.");
            }

            var options = new SessionCreateOptions // Create checkout session
            {
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = "myr",
                            UnitAmount = Convert.ToInt32(plan.Price) * 100,
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = plan.PlanName
                            }
                        },
                        Quantity = 1,
                    }
                },
                Mode = "payment",
                SuccessUrl = "http://localhost:3000/subscription",
                CancelUrl = "http://localhost:3000",

                // Pass metadata to later identify particular user and plan in webhook to create UserSubscription
                Metadata = new Dictionary<string, string>
                {
                    { "UserId", subscriptionDto.UserId.ToString() },
                    { "PlanId", subscriptionDto.PlanId.ToString() }
                }
            };

            var service = new SessionService();

            Session session = service.Create(options);

            return Ok(session.Url);
        }

        // Trigger order fulfillment after payment to create UserSubscription
        [HttpPost("webhook")]
        public async Task<IActionResult> StripeWebhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

            try
            {
                // Verify the webhook signature using the secret from
                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    _stripeSettings.WebhookSecret
                );

                if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted ||
                    stripeEvent.Type == EventTypes.CheckoutSessionAsyncPaymentSucceeded)
                {
                    var session = stripeEvent.Data.Object as Session;

                    if (session != null)
                    {
                        // Retrieve metadata set in the checkout session
                        string userId = session.Metadata.ContainsKey("UserId") ? session.Metadata["UserId"] : null;
                        string planId = session.Metadata.ContainsKey("PlanId") ? session.Metadata["PlanId"] : null;

                        if (int.TryParse(userId, out int uId) && int.TryParse(planId, out int pId))
                        {
                            var userSubscription = new UserSubscription
                            {
                                UserId = uId,
                                PlanId = pId,
                                StartDate = DateTime.UtcNow,
                                EndDate = DateTime.UtcNow.AddMonths(1)
                            };

                            _context.UserSubscription.Add(userSubscription);
                            await _context.SaveChangesAsync();
                        }
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        //[HttpPost("subscribe")]
        //public async Task<IActionResult> Subscribe([FromBody] UserSubscriptionDto subscriptionDto)
        //{
        //    if (subscriptionDto == null)
        //        return BadRequest("Invalid subscription data.");

        //    var userSubscription = new UserSubscription
        //    {
        //        UserId = subscriptionDto.UserId,
        //        PlanId = subscriptionDto.PlanId,
        //        StartDate = subscriptionDto.StartDate,
        //        EndDate = subscriptionDto.EndDate,
        //        IsActive = subscriptionDto.IsActive,
        //    };

        //    _context.UserSubscription.Add(userSubscription);
        //    await _context.SaveChangesAsync();

        //    return Ok(userSubscription);
        //}
    }
}
