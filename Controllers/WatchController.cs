using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Streamflix.DTOs;
using Streamflix.Model;
using Streamflix.Services;
using System.Security.Claims;

namespace Streamflix.Controllers
{
    [ApiController]
    [Route("api/watch")]
    [Authorize]

    public class WatchController : ControllerBase
    {
        private readonly WatchHistoryQueue _queue;

        public WatchController(WatchHistoryQueue queue)
        {
            _queue = queue;
        }

        [HttpPost("update-progress")]
        public IActionResult UpdateProgress([FromBody] WatchHistoryDto historyDto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userId, out var uId) || uId != historyDto.UserId)
            {
                return Unauthorized("You're not authorized to update this progress.");
            }

            _queue.Enqueue(historyDto);

            return Ok("Watch progress update queued.");
        }
    }
}
