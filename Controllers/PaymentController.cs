using Microsoft.AspNetCore.Mvc;

namespace Streamflix.Controllers
{
    public class PaymentController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
