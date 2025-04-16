using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Streamflix.Data;
using Streamflix.DTOs;
using Streamflix.Model;
using Streamflix.Services;
using System.Security.Claims;

namespace Streamflix.Controllers
{
    [ApiController]
    [Route("api/watch")]
    public class WatchController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly WatchHistoryQueue _queue;

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

        [HttpGet("get-progress/{videoTitle}")]
        public async Task<IActionResult> GetProgress(string videoTitle, [FromQuery] int userId)
        {
            if (userId <= 0 || string.IsNullOrEmpty(videoTitle))
            {
                return BadRequest("Invalid user or video title.");
            }

            //var video = await _context.Videos
            //    .Where(v => v.Title == videoTitle)
            //    .FirstOrDefaultAsync();

            //if (video == null)
            //{
            //    return NotFound("Video not found.");
            //}

            var progress = await _context.WatchHistory
                .Where(h => h.UserId == userId && h.VideoTitle == videoTitle)
                .FirstOrDefaultAsync();

            var position = progress?.CurrentPosition ?? 0;

            return Ok(new { currentPosition = position });
        }
    }
}
