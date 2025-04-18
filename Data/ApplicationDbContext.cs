﻿using Microsoft.AspNetCore.Identity;
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
        public DbSet<WatchHistory> WatchHistory { get; set; }
        public DbSet<Genre> Genres { get; set; }
        public DbSet<VideoGenre> VideoGenres { get; set; }
        public DbSet<Actor> Actors { get; set; }
        public DbSet<VideoCast> VideoCasts { get; set; }
        public DbSet<FavoriteVideo> FavoriteVideos { get; set; }

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

            modelBuilder.Entity<Video>()
                .HasIndex(v => v.Title)
                .IsUnique();

            modelBuilder.Entity<Video>()
                .HasAlternateKey(v => v.Title);

            modelBuilder.Entity<FavoriteVideo>()
                .HasOne(f => f.Video)
                .WithMany()
                .HasForeignKey(f => f.VideoTitle)
                .HasPrincipalKey(v => v.Title);

            modelBuilder.Entity<WatchHistory>()
                .HasOne(h => h.Video)
                .WithMany()
                .HasForeignKey(h => h.VideoTitle)
                .HasPrincipalKey(v => v.Title)
                .OnDelete(DeleteBehavior.Cascade);

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
                    Price = 19.90m,
                    FeaturesJson = JsonSerializer.Serialize(new[] { "Unlimited access to movies and TV series", "SD quality", "Ad-free experience", "Cancel anytime" }),
                    Quality = "SD",
                    IsActive = true
                },
                new SubscriptionPlan
                {
                    Id = 2,
                    PlanName = "Standard",
                    Price = 29.90m,
                    FeaturesJson = JsonSerializer.Serialize(new[] { "Unlimited access to movies and TV series", "HD quality", "Ad-free experience", "Cancel anytime" }),
                    Quality = "HD",
                    IsActive = true
                },
                new SubscriptionPlan
                {
                    Id = 3,
                    PlanName = "Premium",
                    Price = 39.90m,
                    FeaturesJson = JsonSerializer.Serialize(new[] {  "Unlimited access to movies and TV series", "4K quality", "Ad-free experience", "Cancel anytime" }),
                    Quality = "4K",
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
            var adminUser = new User
            {
                Id = 1,
                UserName = "admin",
                Email = "admin@gmail.com",
                IsAdmin = true,
                PhoneNumber = "011234567890",
                DateOfBirth = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                PasswordHash = "$2a$11$ygK874fSkPlpFOP0ZgsWQuEDSPZ92jPjyWKNou/GzbYxgjyXSqzCe",
                RegisteredOn = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            };

            modelBuilder.Entity<User>().HasData(adminUser);

            var clientUser = new User
            {
                Id = 2,  
                UserName = "Client",
                Email = "client@gmail.com",
                IsAdmin = false,
                PhoneNumber = "011234567890",
                DateOfBirth = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                PasswordHash = "$2a$11$YhlzICWWwHYr45hL3hvdpeoe10DHG0ebxjk7VdtqQ0nOjLB1c9xYu",
                RegisteredOn = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            };
            modelBuilder.Entity<User>().HasData(clientUser);
        }
    }
}
