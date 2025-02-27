using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Streamflix.Data;
using Streamflix.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
// Add CORS configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin",
        policy => policy.WithOrigins("http://localhost:3000")  // Allow Next.js frontend
                        .AllowAnyMethod()
                        .AllowCredentials() // Allow cookies
                        .AllowAnyHeader());
});
// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DbConnection")));

builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<UserService>();


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
                Console.WriteLine(" Debugging JWT Authentication");

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
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseHttpsRedirection();
// Enable CORS
app.UseCors("AllowSpecificOrigin");  // Add this before app.UseAuthorization()
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();