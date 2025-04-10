using Amazon.Runtime.Internal;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Streamflix.Data;
using Streamflix.DTOs;
using Streamflix.Model;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Streamflix.Controllers
{
    [ApiController]
    [Route("api/videos")]
    [EnableCors("AllowSpecificOrigin")] // Apply the CORS policy here
    public class VideosController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public VideosController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> CreateVideo([FromBody] CreateVideoDto dto)
        {
            try
            {
                Console.WriteLine("👉 Received CreateVideo request");
                Console.WriteLine($"Title: {dto.Title}");
                Console.WriteLine($"Description: {dto.Description}");
                Console.WriteLine($"Duration: {dto.Duration}");
                Console.WriteLine($"MaturityRating: {dto.MaturityRating}");
                Console.WriteLine($"ReleaseDate: {dto.ReleaseDate}");
                Console.WriteLine($"ThumbnailUrl: {dto.ThumbnailUrl}");
                Console.WriteLine($"ContentUrl: {dto.ContentUrl}");
                Console.WriteLine($"Genre: {dto.Genre}");

                var video = new Video
                {
                    Title = dto.Title,
                    Description = dto.Description,
                    Duration = dto.Duration,
                    MaturityRating = dto.MaturityRating,
                    ReleaseDate = DateTime.SpecifyKind(dto.ReleaseDate, DateTimeKind.Utc), // Fix for DateTime kind
                    ThumbnailUrl = string.IsNullOrEmpty(dto.ThumbnailUrl) ? "https://example.com/default-thumbnail.jpg" : dto.ThumbnailUrl,
                    ContentUrl = string.IsNullOrEmpty(dto.ContentUrl) ? "https://example.com/default-video.mp4" : dto.ContentUrl
                };

                // Add video to the database
                _context.Videos.Add(video);
                await _context.SaveChangesAsync();
                Console.WriteLine("✅ Video saved to DB");

                // Find the Genre ID based on the genre name (case-insensitive comparison)
                var genre = await _context.Genres
                    .FirstOrDefaultAsync(g => g.GenreName.ToLower() == dto.Genre.ToLower());

                if (genre == null)
                {
                    return BadRequest("Invalid genre selected.");
                }

                // Create VideoGenre relationship
                var videoGenre = new VideoGenre
                {
                    VideoId = video.Id,
                    GenreId = genre.Id
                };

                // Save the VideoGenre relationship to the database
                _context.VideoGenres.Add(videoGenre);
                await _context.SaveChangesAsync();

                // Save Actors if provided in the request
                if (dto.Actors != null && dto.Actors.Count > 0)
                {
                    foreach (var actorDto in dto.Actors)
                    {
                        // Check if the actor already exists, if not, create a new actor
                        var actor = await _context.Actors
                            .FirstOrDefaultAsync(a => a.Name.ToLower() == actorDto.Name.ToLower());

                        if (actor == null)
                        {
                            actor = new Actor
                            {
                                Name = actorDto.Name,
                                Biography = actorDto.Biography,
                                // Ensure BirthDate is treated as UTC
                                BirthDate = DateTime.SpecifyKind((DateTime)actorDto.BirthDate, DateTimeKind.Utc)
                            };
                            _context.Actors.Add(actor);
                            await _context.SaveChangesAsync(); // Save new actor
                        }

                        // Create VideoCast relationship between video and actor
                        var videoCast = new VideoCast
                        {
                            VideoId = video.Id,
                            ActorId = actor.Id
                        };

                        // Save the VideoCast relationship to the database
                        _context.VideoCasts.Add(videoCast);
                        await _context.SaveChangesAsync(); // Save relationship
                    }
                }


                var jsonOptions = new JsonSerializerOptions
                {
                    ReferenceHandler = ReferenceHandler.Preserve, // Prevent circular reference issues
                    MaxDepth = 64 // Optionally, increase depth if necessary
                };

                // Return the video as JSON response
                var videoResponse = JsonSerializer.Serialize(video, jsonOptions);

                return CreatedAtAction(nameof(GetVideoByTitle), new { title = video.Title }, videoResponse);
            }
            catch (Exception ex)
            {
                Console.WriteLine("🔥 ERROR during CreateVideo");
                Console.WriteLine($"🔥 Exception Type: {ex.GetType().Name}");
                Console.WriteLine($"🔥 Message: {ex.Message}");
                Console.WriteLine($"🔥 StackTrace: {ex.StackTrace}");

                return StatusCode(500, new
                {
                    message = "An error occurred while saving the video.",
                    error = ex.Message,
                    stack = ex.StackTrace
                });
            }
        }
        [HttpGet("title/{title}")]
        public async Task<ActionResult<CreateVideoDto>> GetVideoByTitle(string title)
        {
            var video = await _context.Videos
                .Include(v => v.VideoCasts) // Include actors
                .ThenInclude(vc => vc.Actor) // Include the actor details
                .Include(v => v.VideoGenres) // Include video genres relationship
                .ThenInclude(vg => vg.Genre) // Include genre details
                .FirstOrDefaultAsync(v => v.Title.ToLower() == title.ToLower());

            if (video == null)
            {
                return NotFound(new { message = $"Video with title '{title}' not found." });
            }

            // Map the video to a simpler response DTO
            var videoResponse = new CreateVideoDto
            {
                Title = video.Title,
                Description = video.Description,
                Duration = video.Duration,
                MaturityRating = video.MaturityRating,
                ReleaseDate = video.ReleaseDate, // Keep it in the correct format
                ThumbnailUrl = video.ThumbnailUrl,
                ContentUrl = video.ContentUrl,
                Actors = video.VideoCasts.Select(vc => new ActorDto
                {
                    Name = vc.Actor.Name,
                    Biography = vc.Actor.Biography,
                    BirthDate = vc.Actor.BirthDate
                }).ToList(),
                Genre = string.Join(", ", video.VideoGenres.Select(vg => vg.Genre.GenreName)) // Combine genre names as a comma-separated string
            };

            return Ok(videoResponse);
        }

        [HttpPut("title/{title}")]
        public async Task<IActionResult> UpdateVideo(string title, [FromBody] UpdateVideoDto dto)
        {
            try
            {
                Console.WriteLine($"👉 Received UpdateVideo request for Video Title: {title}");
                Console.WriteLine($"Title: {dto.Title}");
                Console.WriteLine($"Description: {dto.Description}");
                Console.WriteLine($"Duration: {dto.Duration}");
                Console.WriteLine($"MaturityRating: {dto.MaturityRating}");
                Console.WriteLine($"ReleaseDate: {dto.ReleaseDate}");
                Console.WriteLine($"ThumbnailUrl: {dto.ThumbnailUrl}");
                Console.WriteLine($"ContentUrl: {dto.ContentUrl}");
                Console.WriteLine($"Genre: {dto.Genre}");

                // Find the existing video by title
                var video = await _context.Videos
                    .FirstOrDefaultAsync(v => v.Title.ToLower() == title.ToLower());

                if (video == null)
                {
                    return NotFound(new { message = $"Video with title '{title}' not found." });
                }

                // Update video properties
                video.Title = dto.Title ?? video.Title;
                video.Description = dto.Description ?? video.Description;
                video.Duration = dto.Duration > 0 ? dto.Duration : video.Duration;
                video.MaturityRating = dto.MaturityRating ?? video.MaturityRating;
                video.ReleaseDate = dto.ReleaseDate != default ? DateTime.SpecifyKind(dto.ReleaseDate, DateTimeKind.Utc) : video.ReleaseDate;
                video.ThumbnailUrl = !string.IsNullOrEmpty(dto.ThumbnailUrl) ? dto.ThumbnailUrl : video.ThumbnailUrl;
                video.ContentUrl = !string.IsNullOrEmpty(dto.ContentUrl) ? dto.ContentUrl : video.ContentUrl;

                // Save changes to video in the database
                _context.Videos.Update(video);
                await _context.SaveChangesAsync();
                Console.WriteLine("✅ Video updated in DB");

                // Update Genre if changed
                var genre = await _context.Genres
                    .FirstOrDefaultAsync(g => g.GenreName.ToLower() == dto.Genre.ToLower());

                if (genre != null)
                {
                    var videoGenre = await _context.VideoGenres
                        .FirstOrDefaultAsync(vg => vg.VideoId == video.Id);

                    if (videoGenre != null)
                    {
                        // Remove the old relationship
                        _context.VideoGenres.Remove(videoGenre);
                        await _context.SaveChangesAsync();

                        // Create a new relationship
                        var newVideoGenre = new VideoGenre
                        {
                            VideoId = video.Id,
                            GenreId = genre.Id
                        };
                        _context.VideoGenres.Add(newVideoGenre);
                        await _context.SaveChangesAsync();
                    }
                    // else: do nothing
                }
                else
                {
                    return BadRequest("Invalid genre selected.");
                }


                // Update Actors if provided
                if (dto.Actors != null && dto.Actors.Count > 0)
                {
                    // Remove all existing video casts before updating (if you want to replace them)
                    var existingCasts = await _context.VideoCasts
                        .Where(vc => vc.VideoId == video.Id)
                        .ToListAsync();
                    _context.VideoCasts.RemoveRange(existingCasts);
                    await _context.SaveChangesAsync();

                    // Add new actors
                    foreach (var actorDto in dto.Actors)
                    {
                        var actor = await _context.Actors
                            .FirstOrDefaultAsync(a => a.Name.ToLower() == actorDto.Name.ToLower());

                        if (actor == null)
                        {
                            actor = new Actor
                            {
                                Name = actorDto.Name,
                                Biography = actorDto.Biography,
                                BirthDate = DateTime.SpecifyKind((DateTime)actorDto.BirthDate, DateTimeKind.Utc)
                            };
                            _context.Actors.Add(actor);
                            await _context.SaveChangesAsync(); // Save new actor
                        }

                        var videoCast = new VideoCast
                        {
                            VideoId = video.Id,
                            ActorId = actor.Id
                        };

                        _context.VideoCasts.Add(videoCast);
                    }
                    await _context.SaveChangesAsync();
                }

                return Ok(new { message = "Video updated successfully" });
            }
            catch (Exception ex)
            {
                Console.WriteLine("🔥 ERROR during UpdateVideo");
                Console.WriteLine($"🔥 Exception Type: {ex.GetType().Name}");
                Console.WriteLine($"🔥 Message: {ex.Message}");
                Console.WriteLine($"🔥 StackTrace: {ex.StackTrace}");

                return StatusCode(500, new
                {
                    message = "An error occurred while updating the video.",
                    error = ex.Message,
                    stack = ex.StackTrace
                });
            }
        }


        [HttpDelete("title/{title}")]
        public async Task<IActionResult> DeleteVideoByTitle(string title)
        {
            try
            {
                Console.WriteLine($"🗑️ Received DeleteVideo request for Title: {title}");

                var video = await _context.Videos
                    .Include(v => v.VideoGenres)
                    .Include(v => v.VideoCasts)
                    .FirstOrDefaultAsync(v => v.Title.ToLower() == title.ToLower());

                if (video == null)
                {
                    return NotFound(new { message = $"Video with title '{title}' not found." });
                }

                // Delete related VideoGenre entries
                if (video.VideoGenres != null && video.VideoGenres.Any())
                {
                    _context.VideoGenres.RemoveRange(video.VideoGenres);
                }

                // Delete related VideoCast entries
                if (video.VideoCasts != null && video.VideoCasts.Any())
                {
                    _context.VideoCasts.RemoveRange(video.VideoCasts);
                }

                // Delete the video itself
                _context.Videos.Remove(video);
                await _context.SaveChangesAsync();

                Console.WriteLine("✅ Video and all related data deleted successfully");

                return Ok(new { message = $"Video '{title}' deleted successfully." });
            }
            catch (Exception ex)
            {
                Console.WriteLine("🔥 ERROR during DeleteVideo");
                Console.WriteLine($"🔥 Exception Type: {ex.GetType().Name}");
                Console.WriteLine($"🔥 Message: {ex.Message}");
                Console.WriteLine($"🔥 StackTrace: {ex.StackTrace}");

                return StatusCode(500, new
                {
                    message = "An error occurred while deleting the video.",
                    error = ex.Message,
                    stack = ex.StackTrace
                });
            }
        }



    }
}
