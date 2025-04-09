using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Streamflix.Data;
using Streamflix.Model;
using Streamflix.Middleware;
using Streamflix.Services;
using System.Text;
using Amazon;
using Amazon.S3;
using Amazon.Runtime;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);
// Configure HTTPS redirection
builder.Services.AddHttpsRedirection(options =>
{
    options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
    if (builder.Environment.IsDevelopment())
    {
        // Development port from launchSettings.json
        options.HttpsPort = 7230;
    }
    else
    {
        // Production uses standard HTTPS port
        options.HttpsPort = 443;
    }
});

// Add CORS configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin",
        policy => policy.WithOrigins(
                        "http://localhost:3000",  // Development Next.js frontend
                        "https://localhost:3000", // HTTPS local frontend
                        "https://marvelous-panda-93e12f.netlify.app") // Replace with your actual Netlify domain
                    .SetIsOriginAllowed(_ => true)
                    .AllowAnyMethod()
                    .AllowCredentials()
                    .AllowAnyHeader());
});


// Add services to the container.
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));


builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DbConnection")));

builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddHostedService<SubscriptionExpirationService>();
builder.Services.AddSingleton<WatchHistoryQueue>();
builder.Services.AddHostedService<WatchProgressBackgroundService>();


builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                builder.Configuration["JWT:SecretKey"])),
            ValidateIssuer = false,
            ValidateAudience = false,   
            //ValidIssuer = builder.Configuration["JWT:Issuer"],
            //ValidAudience = builder.Configuration["JWT:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };


        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                Console.WriteLine("Debugging JWT Authentication");

                if (context.Request.Cookies.ContainsKey("authToken"))
                {
                    string token = context.Request.Cookies["authToken"];
                    Console.WriteLine(" authToken Found: " + token);
                    context.Token = token;
                }
                else
                {
                    Console.WriteLine("authToken NOT Found");
                    Console.WriteLine(" Available Cookies:");

                    foreach (var cookie in context.Request.Cookies)
                    {
                        Console.WriteLine($" - {cookie.Key}: {cookie.Value}");
                    }

                    Console.WriteLine(" Request Headers:");
                    foreach (var header in context.Request.Headers)
                    {
                        Console.WriteLine($" {header.Key}: {header.Value}");
                    }
                }

                return Task.CompletedTask;
            }
        };
    });


// Register HttpClient
builder.Services.AddHttpClient();

// Register EmailService
builder.Services.AddScoped<EmailService>();
// AWS S3 Configuration
var awsOptions = builder.Configuration.GetSection("AWS");
var awsAccessKey = awsOptions["AccessKey"];
var awsSecretKey = awsOptions["SecretKey"];
var awsSessionToken = awsOptions["SessionToken"];
var awsRegion = awsOptions["Region"];

var credentials = new SessionAWSCredentials(awsAccessKey, awsSecretKey, awsSessionToken);
var s3Client = new AmazonS3Client(credentials, RegionEndpoint.GetBySystemName(awsRegion));

// Add AWS services to DI container
builder.Services.AddSingleton<IAmazonS3>(s3Client);

// Increase file upload size limit (1GB)
const long MaxFileSize = 1L * 1024 * 1024 * 1024; // 1GB

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = MaxFileSize;
});

// Increase max request size for Kestrel
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = MaxFileSize;
});

// If using IIS, ensure it allows large requests
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = MaxFileSize;
});
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });

    // Add JWT Authentication support in Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer <your-token>'"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] { }
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    Console.WriteLine("\n\n\nTru");
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // For API-only applications, use the built-in exception handler middleware
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

// Map a minimal API endpoint for error handling
app.MapGet("/error", () => Results.Problem("An error occurred.", statusCode: 500))
   .ExcludeFromDescription();

app.UseHttpsRedirection();
// Enable CORS
app.UseCors("AllowSpecificOrigin");  // Add this before app.UseAuthorization()
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AdminMiddleware>();
app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
