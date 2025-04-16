using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Streamflix.Data;
using Streamflix.DTOs;
using Streamflix.Model;
using System.ComponentModel;
using System.Security.Claims;

namespace Streamflix.Controllers
{
    [Route("api/user")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public UserController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("get-profile")]
        public async Task<IActionResult> GetUserProfile()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userId, out int uId))
            {
                return Unauthorized("Invalid user ID.");
            }

            var user = await _context.Users
                .Where(u => u.Id == uId && u.IsAdmin == false)
                .FirstOrDefaultAsync();

            if (user != null)
            {
                var activeSubscription = await _context.UserSubscription
                    .Include(us => us.SubscriptionPlan)
                    .Where(us => us.UserId == uId && (us.Status == SubscriptionStatus.Ongoing || us.Status == SubscriptionStatus.Cancelled))
                    .FirstOrDefaultAsync();

                UserProfileDTO userProfileDto = new UserProfileDTO
                {
                    Id = user.Id,
                    Username = user.UserName,
                    Email = user.Email,
                    DateOfBirth = user.DateOfBirth.ToString("yyyy-MM-dd"),
                    PhoneNumber = user.PhoneNumber,
                    RegisteredOn = user.RegisteredOn.ToString("yyyy-MM-dd"),
                    SubscribedPlan = activeSubscription != null ? activeSubscription.SubscriptionPlan.PlanName : ""
                };

                return Ok(userProfileDto);
            }

            return NotFound("User not found");
        }
    }
}
