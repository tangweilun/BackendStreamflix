using Humanizer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Streamflix.Data;
using Streamflix.DTOs;
using Streamflix.Model;
using Stripe;
using Stripe.Checkout;
using System;
using System.Security.Claims;

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
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;


            var plans = await _context.SubscriptionPlans
                .Where(plan => plan.IsActive)
                .OrderBy(plan => plan.Id)
                .ToListAsync();

            return Ok(plans);
        }

        [HttpGet("get-subscribed-plan")]
        [Authorize]
        public async Task<IActionResult> GetSubscribedPlan()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userId, out int uId))
            {
                return Unauthorized("Invalid user ID.");
            }

            var activeSubscription = await _context.UserSubscription
                .Where(us => us.UserId == uId && us.Status == SubscriptionStatus.Ongoing)
                .FirstOrDefaultAsync();

            return Ok(activeSubscription);
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

                // Pass metadata to later identify selected plan in webhook to create UserSubscription
                Metadata = new Dictionary<string, string>
                {
                    { "UserId", subscriptionDto.UserId.ToString() },
                    { "NewPlanId", subscriptionDto.PlanId.ToString() }
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
                        string newPlanId = session.Metadata.ContainsKey("NewPlanId") ? session.Metadata["NewPlanId"] : null;

                        if (int.TryParse(userId, out int uId) && int.TryParse(newPlanId, out int newPId))
                        {
                            // Check whether current user has active subscription
                            var activeSubscription = await _context.UserSubscription
                                .Where(us => us.UserId == uId && us.Status == SubscriptionStatus.Ongoing)
                                .FirstOrDefaultAsync();
                            
                            if (activeSubscription != null) // If user wishes to change plan
                            {
                                var subscribedPlan = await _context.SubscriptionPlans
                                    .Where(p => p.Id == activeSubscription.PlanId && p.IsActive)
                                    .FirstOrDefaultAsync();

                                var newPlan = await _context.SubscriptionPlans
                                    .Where(p => p.Id == newPId && p.IsActive)
                                    .FirstOrDefaultAsync();

                                if (subscribedPlan != null && newPlan != null)
                                {
                                    if (subscribedPlan.Price > newPlan.Price) // If user wishes to downgrade plan
                                    {
                                        var userSubscription = new UserSubscription
                                        {
                                            UserId = uId,
                                            PlanId = newPId,
                                            StartDate = activeSubscription.EndDate, // Lower-priced plan will take effect on next billing date
                                            EndDate = activeSubscription.EndDate.AddMonths(1),
                                            Status = SubscriptionStatus.Pending,
                                        };

                                        _context.UserSubscription.Add(userSubscription);
                                        await _context.SaveChangesAsync();
                                    }
                                    else // If user wishes to upgrade plan
                                    {
                                        DateTime dateTimeNow = DateTime.UtcNow;

                                        activeSubscription.EndDate = dateTimeNow;
                                        activeSubscription.Status = SubscriptionStatus.Expired;

                                        var userSubscription = new UserSubscription
                                        {
                                            UserId = uId,
                                            PlanId = newPId,
                                            StartDate = dateTimeNow, // Higher-priced plan takes effect immediately
                                            EndDate = dateTimeNow.AddMonths(1),
                                            Status = SubscriptionStatus.Ongoing,
                                        };

                                        _context.UserSubscription.Add(userSubscription);
                                        await _context.SaveChangesAsync();
                                    }

                                }

                            }
                            else
                            {
                                var userSubscription = new UserSubscription
                                {
                                    UserId = uId,
                                    PlanId = newPId,
                                    StartDate = DateTime.UtcNow,
                                    EndDate = DateTime.UtcNow.AddMonths(1),
                                    Status = SubscriptionStatus.Ongoing,
                                };

                                _context.UserSubscription.Add(userSubscription);
                                await _context.SaveChangesAsync();
                            }
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
