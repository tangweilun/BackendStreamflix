using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Streamflix.Data;
using Streamflix.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Streamflix.Controllers
{
    [Route("api/favorite-videos")]
    [ApiController]
    [EnableCors("AllowSpecificOrigin")]
    public class FavoriteVideosController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public FavoriteVideosController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/favorite-videos
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Video>>> GetFavoriteVideos([FromQuery] int userId)
        {
            if (userId <= 0)
            {
                return BadRequest("Invalid user ID");
            }

            var favorites = await _context.FavoriteVideos
                .Where(f => f.UserId == userId)
                .Include(f => f.Video)
                .Select(f => f.Video)
                .ToListAsync();

            return favorites;
        }

        // POST: api/favorite-videos/{videoTitle}
        [HttpPost("{videoTitle}")]
        public async Task<ActionResult> AddToFavorites(string videoTitle, [FromBody] UserIdRequest request)
        {
            if (request.UserId <= 0)
            {
                return BadRequest("Invalid user ID");
            }

            var videoExists = await _context.Videos.AnyAsync(v => v.Title == videoTitle);
            if (!videoExists)
            {
                return NotFound("Video not found");
            }

            var existingFavorite = await _context.FavoriteVideos
                .FirstOrDefaultAsync(f => f.UserId == request.UserId && f.VideoTitle == videoTitle);

            if (existingFavorite != null)
            {
                return BadRequest("Video is already in favorites");
            }

            var favorite = new FavoriteVideo
            {
                UserId = request.UserId,
                VideoTitle = videoTitle,
                DateAdded = DateTime.UtcNow
            };

            _context.FavoriteVideos.Add(favorite);
            await _context.SaveChangesAsync();

            return Ok();
        }

        // DELETE: api/favorite-videos/{videoTitle}
        [HttpDelete("{videoTitle}")]
        public async Task<ActionResult> RemoveFromFavorites(string videoTitle, [FromQuery] int userId)
        {
            if (userId <= 0)
            {
                return BadRequest("Invalid user ID");
            }

            var favorite = await _context.FavoriteVideos
                .FirstOrDefaultAsync(f => f.UserId == userId && f.VideoTitle == videoTitle);

            if (favorite == null)
            {
                return NotFound("Video not found in favorites");
            }

            _context.FavoriteVideos.Remove(favorite);
            await _context.SaveChangesAsync();

            return Ok();
        }

        // GET: api/favorite-videos/check/{videoTitle}
        [HttpGet("check/{videoTitle}")]
        public async Task<ActionResult<bool>> CheckFavorite(string videoTitle, [FromQuery] int userId)
        {
            if (userId <= 0)
            {
                return BadRequest("Invalid user ID");
            }

            var isFavorite = await _context.FavoriteVideos
                .AnyAsync(f => f.UserId == userId && f.VideoTitle == videoTitle);

            return isFavorite;
        }
    }

    public class UserIdRequest
    {
        public int UserId { get; set; }
    }
}