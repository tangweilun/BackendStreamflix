using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Streamflix.Data;
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

        [HttpPost("pay")]
        public IActionResult Pay([FromBody] string price)
        {
            StripeConfiguration.ApiKey = _stripeSettings.SecretKey;

            var options = new SessionCreateOptions // Create checkout session
            {
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        Price = price,
                        Quantity = 1,
                    }
                },
                Mode = "payment",
                SuccessUrl = "http://localhost:3000/subscription",
                CancelUrl = "http://localhost:3000"
            };

            var service = new SessionService();

            Session session = service.Create(options);

            return Ok(session.Url);
        }

        //[HttpPost("create-customer")]
        //public async Task<dynamic> CreateCustomer([FromBody] User userInfo)
        //{
        //    StripeConfiguration.ApiKey = _stripeSettings.SecretKey;

        //    var userOptions = new CustomerCreateOptions
        //    {
        //        Email = userInfo.Email,
        //        Name = userInfo.UserName
        //    };

        //    var user = await _customerService.CreateAsync(userOptions);

        //    // Create users object in the application database

        //    return new { user };
        //}
    }
}
