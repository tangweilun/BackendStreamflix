using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Streamflix.Model;
using System.Diagnostics;
using Stripe;
using Stripe.Checkout;

namespace Streamflix.Controllers
{
    //[ApiController]
    //[Route("api/admin")]
    public class PaymentController : Controller
    {
        private readonly StripeSettings _stripeSettings;

        public PaymentController(IOptions<StripeSettings> stripeSettings)
        {
            _stripeSettings = stripeSettings.Value;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult CreateCheckoutSession(string amount)
        {
            var currency = "myr";
            var successUrl = "https://localhost:44320/Home/success";
            var cancelUrl = "https://localhost:44320/Home/cancel";
            StripeConfiguration.ApiKey = _stripeSettings.SecretKey;

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string>
            {
                "card"
            },
                LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = currency,
                        UnitAmount = Convert.ToInt32(amount) * 100,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = "Product Name",
                            Description = "Product Description"
                        }
                    },
                    Quantity = 1
                }
            },
                Mode = "payment",
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl
            };

            var service = new SessionService();
            var session = service.Create(options);

            return Redirect(session.Url);
        }

        public async Task<IActionResult> success()
        {
            return View("Index");
        }

        public IActionResult cancel()
        {
            return View("Index");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
