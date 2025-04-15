using Microsoft.AspNetCore.Mvc;

namespace Streamflix.Controllers
{
    [Route("api/helloworld")]
    [ApiController]
    public class HelloWorldController : ControllerBase
    {
        // GET api/helloworld
        [HttpGet]
        public IActionResult Get()
        {
            return Ok("Hello World");
        }
    }
}
