using Microsoft.AspNetCore.Mvc;

namespace Streamflix.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HellowWorldController : ControllerBase
    {
        // GET api/helloworld
        [HttpGet]
        public IActionResult Get()
        {
            return Ok("Hello World");
        }
    }
}
