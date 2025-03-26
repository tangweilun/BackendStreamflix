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
        public DbSet<Content> Content { get; set; }
        public DbSet<WatchList> WatchLists { get; set; }
        public DbSet<Genre> Genres { get; set; }
        public DbSet<ContentGenre> ContentGenres { get; set; }
        public DbSet<Actor> Actors { get; set; }
        public DbSet<ContentCast> ContentCasts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure relationship between User and PasswordResetToken
            modelBuilder.Entity<PasswordResetToken>()
                .HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ContentGenre>()
                .HasKey(cg => new { cg.ContentId, cg.GenreId }); // Composite Key

            modelBuilder.Entity<ContentCast>()
                .HasKey(cc => new { cc.ContentId, cc.ActorId }); // Composite Key

            modelBuilder.Entity<SubscriptionPlan>()
                .Property(e => e.FeaturesJson)
                .HasColumnType("jsonb");
        }
    }
}
