using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Streamflix.Model;
using Streamflix.Model.Streamflix.Model;
using System.Text.Json;

namespace Streamflix.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
        public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
        public DbSet<UserSubscription> UserSubscription { get; set; }
        public DbSet<Video> Videos { get; set; }
        public DbSet<WatchList> WatchLists { get; set; }
        public DbSet<WatchHistory> WatchHistory { get; set; }
        public DbSet<Genre> Genres { get; set; }
        public DbSet<VideoGenre> VideoGenres { get; set; }
        public DbSet<Actor> Actors { get; set; }
        public DbSet<VideoCast> VideoCasts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Existing relationship configurations
            modelBuilder.Entity<PasswordResetToken>()
                .HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<VideoGenre>()
                .HasKey(cg => new { cg.VideoId, cg.GenreId });

            modelBuilder.Entity<VideoCast>()
                .HasKey(cc => new { cc.VideoId, cc.ActorId });

            modelBuilder.Entity<SubscriptionPlan>()
                .Property(e => e.FeaturesJson)
                .HasColumnType("jsonb");

            modelBuilder.Entity<UserSubscription>()
                .Property(u => u.Status)
                .HasConversion<string>();

            // Seed data
            SeedData(modelBuilder);
        }

        private void SeedData(ModelBuilder modelBuilder)
        {
            // Seed subscription plans
            modelBuilder.Entity<SubscriptionPlan>().HasData(
                new SubscriptionPlan
                {
                    Id = 1,
                    PlanName = "Basic",
                    Price = 40.00m,
                    FeaturesJson = JsonSerializer.Serialize(new[] { "SD Quality", "1 Screen" }),
                    Quality = "SD",
                    MaxStreams = 1,
                    IsActive = true
                },
                new SubscriptionPlan
                {
                    Id = 2,
                    PlanName = "Standard",
                    Price = 14.99m,
                    FeaturesJson = JsonSerializer.Serialize(new[] { "HD Quality", "2 Screens", "Downloads" }),
                    Quality = "HD",
                    MaxStreams = 2,
                    IsActive = true
                },
                new SubscriptionPlan
                {
                    Id = 3,
                    PlanName = "Premium",
                    Price = 19.99m,
                    FeaturesJson = JsonSerializer.Serialize(new[] { "4K Quality", "4 Screens", "Downloads", "No Ads" }),
                    Quality = "4K",
                    MaxStreams = 4,
                    IsActive = true
                }
            );

            // Seed genres
            modelBuilder.Entity<Genre>().HasData(
                new Genre { Id = 1, GenreName = "Action" },
                new Genre { Id = 2, GenreName = "Comedy" },
                new Genre { Id = 3, GenreName = "Drama" },
                new Genre { Id = 4, GenreName = "Horror" },
                new Genre { Id = 5, GenreName = "Sci-Fi" },
                new Genre { Id = 6, GenreName = "Thriller" },
                new Genre { Id = 7, GenreName = "Documentary" },
                new Genre { Id = 8, GenreName = "Animation" }
            );

            // Seed admin user
            var passwordHasher = new PasswordHasher<User>();
            var adminUser = new User
            {
                Id = 1, // Set a specific ID for seeding
                UserName = "admin",
                Email = "admin@gmail.com",
                IsAdmin = true, // Assuming your User model has an IsAdmin property
                PhoneNumber = "011234567890",
                // Other required non-nullable properties
                DateOfBirth = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                PasswordHash = "$2a$11$ygK874fSkPlpFOP0ZgsWQuEDSPZ92jPjyWKNou/GzbYxgjyXSqzCe"

            };

            modelBuilder.Entity<User>().HasData(adminUser);



        }

        }



}
