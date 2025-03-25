using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Streamflix.Data;
using Streamflix.DTOs;
using Streamflix.Model;
using Streamflix.Services;
using System.Security.Cryptography;
using Streamflix.Model.Streamflix.Model;

namespace Streamflix.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly TokenService _tokenService;
        private readonly EmailService _emailService;
        private readonly IConfiguration _configuration;

        public AuthController(ApplicationDbContext context, TokenService tokenService, EmailService emailService, IConfiguration configuration)
        {
            _context = context;
            _tokenService = tokenService;
            _emailService = emailService;
            _configuration = configuration;

        }

        [HttpPost("register")]
        public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto registerDto)
        {
            if (await _context.Users.AnyAsync(x => x.Email == registerDto.Email))
            {
                return BadRequest("Email already exists");
            }
            // Create password hash
            using var hmac = new HMACSHA512();
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password);
            var user = new User
            {
                UserName = registerDto.UserName,
                Email = registerDto.Email,
                PasswordHash = passwordHash,
                DateOfBirth = registerDto.DateOfBirth,
                PhoneNumber = registerDto.PhoneNumber,
                IsAdmin = false
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            var token = _tokenService.CreateToken(user);
            return new AuthResponseDto
            {
                Token = token,
                UserName = user.UserName,
                Email = user.Email,
                IsAdmin = user.IsAdmin
            };
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponseDto>> Login(LoginDto loginDto)
        {
            var user = await _context.Users.SingleOrDefaultAsync(x => x.Email == loginDto.Email);
            if (user == null)
            {
                return Unauthorized("Invalid email or password");
            }
            var isValid = BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash);
            if (!isValid)
            {
                return Unauthorized("Invalid email or password");
            }
            var token = _tokenService.CreateToken(user);
            return new AuthResponseDto
            {
                Token = token,
                UserName = user.UserName,
                Email = user.Email,
                IsAdmin = user.IsAdmin
            };
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDto forgotPasswordDto)
        {
            // For security, don't reveal whether the email exists or not
            var user = await _context.Users.SingleOrDefaultAsync(x => x.Email == forgotPasswordDto.Email);
            if (user == null)
            {
                // Return success response even if user doesn't exist
                return Ok(new { message = "If an account with that email exists, we've sent password reset instructions." });
            }

            // Generate a reset token
            var resetToken = GenerateResetToken();
            var tokenExpiryTime = DateTime.UtcNow.AddHours(1);

            // Store the reset token in the database
            var resetPasswordToken = new PasswordResetToken
            {
                UserId = user.Id,
                Token = resetToken,
                ExpiryDate = tokenExpiryTime
            };

            // Check if the user already has a token and update it
            var existingToken = await _context.PasswordResetTokens.FirstOrDefaultAsync(t => t.UserId == user.Id);
            if (existingToken != null)
            {
                existingToken.Token = resetToken;
                existingToken.ExpiryDate = tokenExpiryTime;
                _context.PasswordResetTokens.Update(existingToken);
            }
            else
            {
                _context.PasswordResetTokens.Add(resetPasswordToken);
            }

            await _context.SaveChangesAsync();

            // Get the client application URL from configuration
            var clientUrl = _configuration["JWT:Audience"] ?? "http://localhost:3000";
            var resetUrl = $"{clientUrl}/reset-password?token={resetToken}";

            // Send reset email using the email service
            await _emailService.SendPasswordResetEmail(user.Email, resetUrl);

            return Ok(new { message = "If an account with that email exists, we've sent password reset instructions." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto resetPasswordDto)
        {
            // Validate the token
            var resetToken = await _context.PasswordResetTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Token == resetPasswordDto.Token && t.ExpiryDate > DateTime.UtcNow);

            if (resetToken == null)
            {
                return BadRequest("Invalid or expired token.");
            }

            var user = resetToken.User;

            // Update the user's password
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(resetPasswordDto.Password);
            _context.Users.Update(user);

            // Remove the used token
            _context.PasswordResetTokens.Remove(resetToken);

            await _context.SaveChangesAsync();

            return Ok(new { message = "Password has been reset successfully." });
        }

        private string GenerateResetToken()
        {
            // Generate a cryptographically secure random token
            var randomBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            return Convert.ToBase64String(randomBytes)
                .Replace("/", "_")
                .Replace("+", "-")
                .Replace("=", "");
        }
    }
}