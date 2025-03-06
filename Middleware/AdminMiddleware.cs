using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Streamflix.Middleware
{
    public class AdminMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _config;

        public AdminMiddleware(RequestDelegate next, IConfiguration config)
        {
            _next = next;
            _config = config;
        }

        public async Task Invoke(HttpContext context)
        {
            string token = null;
            if (context.Request.Cookies.ContainsKey("authToken"))
            {
                token = context.Request.Cookies["authToken"];
                Console.WriteLine("[AdminMiddleware] authToken found: " + token);
            }
            else
            {
                Console.WriteLine("[AdminMiddleware] No authToken found in cookies");
            }

            string roleClaim = null;
            if (!string.IsNullOrEmpty(token))
            {
                var handler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_config["JWT:SecretKey"]);
                try
                {
                    var tokenValidationParams = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(key),
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    };

                    var principal = handler.ValidateToken(token, tokenValidationParams, out _);
                    roleClaim = principal.FindFirst(ClaimTypes.Role)?.Value;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[AdminMiddleware] Token validation failed: " + ex.Message);
                }
            }

            Console.WriteLine("[AdminMiddleware] Request Path: " + context.Request.Path);
            Console.WriteLine("[AdminMiddleware] User Role: " + (roleClaim ?? "No Role Found"));

            if (context.Request.Path.StartsWithSegments("/api/admin"))
            {
                if (string.IsNullOrEmpty(roleClaim) || roleClaim != "Admin")
                {
                    Console.WriteLine("[AdminMiddleware] Access Denied: User is not an Admin");
                    context.Response.StatusCode = StatusCodes.Status403Forbidden; // Explicitly setting Forbidden status
                    await context.Response.WriteAsync("Access Denied: Admins only.");
                    return; // Ensure request execution stops
                }
                Console.WriteLine("[AdminMiddleware] Access Granted: User is an Admin");
            }

            await _next(context);
        }

    }
}
