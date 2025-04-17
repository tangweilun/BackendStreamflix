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
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000); // Listen on port 5000 for all network interfaces
});

// Add CORS configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin",
        policy => policy.WithOrigins("http://localhost:3000")  // Allow Next.js frontend
                        .SetIsOriginAllowed(_ => true)  // Allows dynamic origins if needed
                        .AllowAnyMethod()
                        .AllowCredentials() // Allow cookies
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
// AWS S3 Configuration
try
{
    var awsOptions = builder.Configuration.GetSection("AWS");
    var awsAccessKey = awsOptions["AccessKey"];
    var awsSecretKey = awsOptions["SecretKey"];
    var awsSessionToken = awsOptions["SessionToken"];
    var awsRegion = awsOptions["Region"] ?? "us-east-1";

    Console.WriteLine($"Configuring AWS S3 with region: {awsRegion}");

    AmazonS3Client s3Client;

    if (!string.IsNullOrEmpty(awsAccessKey) && !string.IsNullOrEmpty(awsSecretKey))
    {
        Console.WriteLine("Using provided AWS credentials");
        
        if (string.IsNullOrEmpty(awsSessionToken))
        {
            // Use basic credentials without session token
            var basicCredentials = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
            s3Client = new AmazonS3Client(basicCredentials, RegionEndpoint.GetBySystemName(awsRegion));
        }
        else
        {
            // Use session credentials with token
            var sessionCredentials = new SessionAWSCredentials(awsAccessKey, awsSecretKey, awsSessionToken);
            s3Client = new AmazonS3Client(sessionCredentials, RegionEndpoint.GetBySystemName(awsRegion));
        }
    }
    else
    {
        Console.WriteLine("No AWS credentials provided, falling back to instance profile or default credentials");
        // Use default credentials provider chain - will use instance profile if running on EC2
        s3Client = new AmazonS3Client(RegionEndpoint.GetBySystemName(awsRegion));
    }

    // Optionally test the connection (comment out if not needed)
    try
    {
        var response = s3Client.ListBucketsAsync().GetAwaiter().GetResult();
        Console.WriteLine($"Successfully connected to S3. Found {response.Buckets.Count} buckets.");
    }
    catch (Exception testEx)
    {
        Console.WriteLine($"Warning: Could not connect to S3: {testEx.Message}");
        // Continue anyway, as we've created the client
    }

    // Add AWS services to DI container
    builder.Services.AddSingleton<IAmazonS3>(s3Client);
    Console.WriteLine("AWS S3 client registered successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"Error initializing AWS S3 client: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    
    // Optional: Register a mock/dummy S3 client or handle the error differently
    // builder.Services.AddSingleton<IAmazonS3>(new MockAmazonS3Client());
    
    // Depending on your requirements, you might want to:
    // 1. Continue without S3 functionality
    // 2. Use a mock implementation
    // 3. Throw the exception and prevent application startup
    
    // Option 3: Rethrow if S3 is critical for your application
    // throw;
}

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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    Console.WriteLine("\n\n\nTru");

    app.UseSwagger();
    app.UseSwaggerUI();

    app.UseExceptionHandler("/Home/Error");
    //The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka/ms/aspnetcore-hsts
    app.UseHsts();
}


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
