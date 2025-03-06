    using Microsoft.Extensions.Configuration;
    using System;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    namespace Streamflix.Services
    {
        public class EmailService
        {
            private readonly HttpClient _httpClient;
            private readonly string _resendApiKey;
            private readonly string _fromEmail;

            public EmailService(IConfiguration configuration, HttpClient httpClient)
            {
                _httpClient = httpClient;
                _resendApiKey = configuration["Resend:ApiKey"];
                _fromEmail = configuration["Resend:FromEmail"] ?? "onboarding@resend.dev";
            }

            public async Task SendPasswordResetEmail(string toEmail, string resetUrl)
            {
                var emailContent = new
                {
                    from = _fromEmail,
                    to = toEmail,
                    subject = "Reset Your Password",
                    html = $@"
                <div style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;"">
                  <h2 style=""color: #f97316;"">Reset Your Password</h2>
                  <p>We received a request to reset your password. Click the button below to create a new password:</p>
                  <div style=""text-align: center; margin: 30px 0;"">
                    <a href=""{resetUrl}"" style=""background-color: #f97316; color: white; padding: 12px 24px; text-decoration: none; border-radius: 4px; display: inline-block; font-weight: bold;"">Reset Password</a>
                  </div>
                  <p>This link will expire in 1 hour.</p>
                  <p>If you did not request a password reset, please ignore this email.</p>
                  <p style=""color: #666; font-size: 12px; margin-top: 30px;"">
                    This is an automated email, please do not reply.
                  </p>
                </div>
                "
                };

                var json = JsonSerializer.Serialize(emailContent);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_resendApiKey}");

                var response = await _httpClient.PostAsync("https://api.resend.com/emails", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to send email: {errorContent}");
                }
            }
        }
    }
