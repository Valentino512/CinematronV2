using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Cinematron.Models;

namespace Cinematron.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<Cinematron.Models.ApplicationUser>(options)
    {
        public DbSet<Movie> Movies => Set<Movie>();

        public DbSet<MovieFile> MovieFiles => Set<MovieFile>();

        public DbSet<Comment> Comments => Set<Comment>();
        public DbSet<Message> Messages => Set<Message>();
        public DbSet<Joke> Jokes => Set<Joke>();
        public DbSet<NewsItem> NewsItems => Set<NewsItem>();
        public DbSet<VideoReaction> VideoReactions => Set<VideoReaction>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.ConfigureWarnings(warnings =>
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Cinematron.Models.ApplicationUser>(entity =>
            {
                entity.Property(user => user.FullName).HasMaxLength(120);
                entity.Property(user => user.Age);
                entity.Property(user => user.Gender).HasMaxLength(40);
                entity.Property(user => user.ProfileMemo).HasMaxLength(2000);
            });

            builder.Entity<Movie>(entity =>
            {
                entity.HasKey(movie => movie.Id);
                entity.Property(movie => movie.Title).HasMaxLength(100).IsRequired();
                entity.Property(movie => movie.Genre).HasMaxLength(60).IsRequired();
                entity.Property(movie => movie.Description).HasMaxLength(1000).IsRequired();
                entity.Property(movie => movie.OwnerId).HasMaxLength(450).IsRequired();
                entity.Property(movie => movie.CreatedUtc).HasPrecision(0);
                entity.Property(movie => movie.IsPublic).IsRequired().HasDefaultValue(true);
                entity.HasIndex(movie => movie.Title);
                entity.HasIndex(movie => movie.OwnerId);
                entity.HasOne(movie => movie.Owner)
                    .WithMany()
                    .HasForeignKey(movie => movie.OwnerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Comment>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Text).HasMaxLength(2000).IsRequired();
                entity.Property(c => c.UserId).HasMaxLength(450).IsRequired();
                entity.Property(c => c.CreatedUtc).HasPrecision(0);
                entity.Property(c => c.IsHighlighted).IsRequired();
                entity.HasOne(c => c.Movie)
                    .WithMany(m => m.Comments)
                    .HasForeignKey(c => c.MovieId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Joke>(entity =>
            {
                entity.HasKey(joke => joke.Id);
                entity.Property(joke => joke.Text).HasMaxLength(1000).IsRequired();
                entity.Property(joke => joke.Author).HasMaxLength(120);
                entity.Property(joke => joke.PublishedUtc).HasPrecision(0);
            });

            builder.Entity<NewsItem>(entity =>
            {
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Headline).HasMaxLength(200).IsRequired();
                entity.Property(item => item.Summary).HasMaxLength(2000).IsRequired();
                entity.Property(item => item.Source).HasMaxLength(200);
                entity.Property(item => item.PublishedUtc).HasPrecision(0);
            });

            builder.Entity<VideoReaction>(entity =>
            {
                entity.HasKey(reaction => reaction.Id);
                entity.Property(reaction => reaction.UserId).HasMaxLength(450).IsRequired();
                entity.Property(reaction => reaction.Type).IsRequired();
                entity.HasIndex(reaction => new { reaction.MovieId, reaction.UserId }).IsUnique();
                entity.HasOne(reaction => reaction.Movie)
                    .WithMany(movie => movie.Reactions)
                    .HasForeignKey(reaction => reaction.MovieId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<MovieFile>(entity =>
            {
                entity.HasKey(file => file.Id);
                entity.Property(file => file.AssetType).HasMaxLength(20).IsRequired();
                entity.Property(file => file.StoragePath).HasMaxLength(1024).IsRequired();
                entity.Property(file => file.OriginalFileName).HasMaxLength(255).IsRequired();
                entity.Property(file => file.ContentType).HasMaxLength(100).IsRequired();
                entity.Property(file => file.UploadedUtc).HasPrecision(0);
                entity.HasIndex(file => new { file.MovieId, file.AssetType });
                entity.HasOne(file => file.Movie)
                    .WithMany(movie => movie.Files)
                    .HasForeignKey(file => file.MovieId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
