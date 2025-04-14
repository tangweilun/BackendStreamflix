using Amazon.S3.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Streamflix.Data;
using Streamflix.DTOs;
using Streamflix.Model;
using Streamflix.Services;
using Stripe.Forwarding;
using System.Security.Claims;

namespace Streamflix.Controllers
{
    [ApiController]
    [Route("api/watch")]
    [Authorize]

    public class WatchController : ControllerBase
    {
        private readonly WatchHistoryQueue _queue;
        private readonly ApplicationDbContext _context;

        public WatchController(ApplicationDbContext context, WatchHistoryQueue queue)
        {
            _context = context;
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

        [HttpPost("get-progress")]
        public async Task<IActionResult> GetProgress([FromBody] WatchHistoryDto historyDto)
        {
            if (historyDto.UserId <= 0 || historyDto.VideoId <= 0)
            {
                return BadRequest("Invalid user or video ID.");
            }

            var progress = await _context.WatchHistory
                .Where(h => h.UserId == historyDto.UserId && h.VideoId == historyDto.VideoId)
                .FirstOrDefaultAsync();

            if (progress == null)
            {
                return NotFound("No progress found.");
            }

            return Ok(progress);
        }
    }
}
