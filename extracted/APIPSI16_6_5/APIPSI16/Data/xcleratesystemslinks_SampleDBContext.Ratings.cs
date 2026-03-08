using Microsoft.EntityFrameworkCore;
using APIPSI16.Models;

namespace APIPSI16.Data
{
    public partial class xcleratesystemslinks_SampleDBContext
    {
        public virtual DbSet<Rating> Ratings { get; set; }
    }

    // Rating entity configuration via the partial OnModelCreatingPartial hook,
    // since Rating was added after the initial scaffold and is not in the main OnModelCreating.
    public partial class xcleratesystemslinks_SampleDBContext
    {
        partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Rating>(entity =>
            {
                entity.HasKey(e => e.RatingId);

                entity.ToTable("Ratings");

                entity.Property(e => e.EntityType)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.Review)
                    .HasMaxLength(1000);

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime2");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("datetime2");

                entity.HasOne(d => d.RatedByUser)
                    .WithMany()
                    .HasForeignKey(d => d.RatedByUserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
