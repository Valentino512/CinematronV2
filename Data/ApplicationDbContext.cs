using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Cinematron.Models;

namespace Cinematron.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
    {
        public DbSet<Movie> Movies => Set<Movie>();

        public DbSet<MovieFile> MovieFiles => Set<MovieFile>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Movie>(entity =>
            {
                entity.HasKey(movie => movie.Id);
                entity.Property(movie => movie.Title).HasMaxLength(100).IsRequired();
                entity.Property(movie => movie.Genre).HasMaxLength(60).IsRequired();
                entity.Property(movie => movie.Description).HasMaxLength(1000).IsRequired();
                entity.Property(movie => movie.CreatedUtc).HasPrecision(0);
                entity.HasIndex(movie => movie.Title);
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
