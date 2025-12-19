using Microsoft.EntityFrameworkCore;
using RemittanceAdviceManager.Models;

namespace RemittanceAdviceManager.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<RemittanceFile> RemittanceFiles { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<RemittanceFile>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FileName).IsRequired();
                entity.Property(e => e.FilePath).IsRequired();
                entity.Property(e => e.DownloadedDate).IsRequired();
                entity.Property(e => e.Status)
                    .IsRequired()
                    .HasConversion<string>();
                entity.Property(e => e.CreatedAt).IsRequired();

                // IsSelected is not stored in database
                entity.Ignore(e => e.IsSelected);
            });
        }
    }
}
