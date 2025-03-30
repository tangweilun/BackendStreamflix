using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Streamflix.DTOs;
using Streamflix.Services;
using System.Security.Claims;

namespace Streamflix.Controllers
{
    [ApiController]
    [Route("api/watch")]
    [Authorize]

    public class WatchController : ControllerBase
    {
        private readonly IWatchHistoryQueue _queue;

        public WatchController(IWatchHistoryQueue queue)
        {
            _queue = queue;
        }

        [HttpPost("update-progress")]
        public IActionResult UpdateProgress([FromBody] WatchProgressUpdateDto updateDto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userId, out var uId) || uId != updateDto.UserId)
            {
                return Unauthorized("You're not authorized to update this progress.");
            }

            _queue.Enqueue(updateDto);

            return Ok("Watch progress update queued.");
        }
    }
}
