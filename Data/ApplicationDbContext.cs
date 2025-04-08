using Microsoft.EntityFrameworkCore;
using Streamflix.Model;
using Streamflix.Model.Streamflix.Model;

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

            // Configure relationship between User and PasswordResetToken
            modelBuilder.Entity<PasswordResetToken>()
                .HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<VideoGenre>()
                .HasKey(cg => new { cg.VideoId, cg.GenreId }); // Composite Key

            modelBuilder.Entity<VideoCast>()
                .HasKey(cc => new { cc.VideoId, cc.ActorId }); // Composite Key

            modelBuilder.Entity<SubscriptionPlan>()
                .Property(e => e.FeaturesJson)
                .HasColumnType("jsonb");

            modelBuilder.Entity<UserSubscription>()
                .Property(u => u.Status)
                .HasConversion<string>();
        }
    }
}
